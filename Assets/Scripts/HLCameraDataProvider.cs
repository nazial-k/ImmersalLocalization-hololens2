/*===============================================================================
Copyright (C) 2024 Immersal Ltd. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sdk@immersal.com for licensing requests.
===============================================================================*/

using UnityEngine;
using System;
using System.Linq;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Threading;
using System.Threading.Tasks;

#if ENABLE_WINMD_SUPPORT
using Windows.Perception.Spatial;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.Capture;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.MixedReality.OpenXR;
#endif

namespace Immersal.AR
{
    public struct CameraData
    {
        public byte[] pixels;
        public int width;
        public int height;
        public Vector2 principalPoint;
        public Vector2 focalLength;
        public Pose cameraPose;
#if ENABLE_WINMD_SUPPORT
        public SpatialCoordinateSystem coordinateSystem;
#endif
    }

    public class HLCameraDataProvider : MonoBehaviour, ICameraDataProvider
    {
        [SerializeField] private float captureInterval = 1.0f;

		private SynchronizationContext m_Context;
        private bool m_FrameReceived = false;
        private CameraData m_CameraData;

#if ENABLE_WINMD_SUPPORT
        private Stopwatch m_Stopwatch = new Stopwatch();
        private TimeSpan m_LatestTime = TimeSpan.MinValue;

        private readonly SemaphoreSlim m_Semaphore = new SemaphoreSlim(1, 1);
        private bool m_IsCapturing = false;

        private MediaCapture m_MediaCapture;
        private MediaFrameReader m_MediaFrameReader;

        private byte[] m_GrayBytes;
        private byte[] m_RGBBytes;

        private SpatialCoordinateSystem WorldOriginCoordinateSystem
        {
            get
            {
                return PerceptionInterop.GetSceneCoordinateSystem(UnityEngine.Pose.identity) as SpatialCoordinateSystem;
            }
        }
#endif

        public void Start()
        {
			m_Context = SynchronizationContext.Current;
		}

        public void StartCapture()
        {
#if ENABLE_WINMD_SUPPORT
            _ = StartCaptureAsync();
#else
            throw new NotImplementedException();
#endif
        }

        public void StopCapture()
        {
#if ENABLE_WINMD_SUPPORT
            _ = StopCaptureAsync();
#else
            throw new NotImplementedException();
#endif
        }

        public bool TryAcquireLatestData(out byte[] pixels, out int width, out int height, out Vector4 intrinsics, out Pose cameraPose)
        {
            pixels = null;
            width = 0;
            height = 0;
            intrinsics = Vector4.zero;
            cameraPose = default;

            if (!m_FrameReceived) return false;

            pixels = m_CameraData.pixels;
            width = m_CameraData.width;
            height = m_CameraData.height;
            cameraPose = m_CameraData.cameraPose;
            intrinsics.x = m_CameraData.focalLength.x;
            intrinsics.y = m_CameraData.focalLength.y;
            intrinsics.z = m_CameraData.principalPoint.x;
            intrinsics.w = m_CameraData.principalPoint.y;

            return true;
        }

#if ENABLE_WINMD_SUPPORT
        private async Task StopCaptureAsync()
        {
            await m_Semaphore.WaitAsync();

            try
            {
                if (m_IsCapturing)
                {
                    m_MediaFrameReader.FrameArrived -= ColorFrameReader_FrameArrived;
                    await m_MediaFrameReader.StopAsync();
                    m_MediaCapture.Dispose();
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                m_MediaCapture = null;
                m_IsCapturing = false;
                m_Semaphore.Release();
            }
        }

        private async Task StartCaptureAsync()
        {
            await m_Semaphore.WaitAsync();

            try
            {
                if (m_IsCapturing)
                {
                    return;
                }

                var (selectedGroup, colorSourceInfo) = await FindVideoPreviewSource();
                if (selectedGroup == null || colorSourceInfo == null)
                {
                    return;
                }

                m_MediaCapture = await InitializeMediaCapture(selectedGroup);

                if (m_MediaCapture == null)
                {
                    return;
                }
                var colorFrameSource = m_MediaCapture.FrameSources[colorSourceInfo.Id];
                await SetFormat(colorFrameSource);

                m_MediaFrameReader = await m_MediaCapture.CreateFrameReaderAsync(colorFrameSource, MediaEncodingSubtypes.Argb32);
                m_MediaFrameReader.FrameArrived += ColorFrameReader_FrameArrived;

                m_Stopwatch.Start();
                await m_MediaFrameReader.StartAsync();
            }
            finally
            {
                m_Semaphore.Release();
            }
        }

        private async Task<(MediaFrameSourceGroup sourceGroup, MediaFrameSourceInfo sourceInfo)> FindVideoPreviewSource()
        {
            var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();

            foreach (var sourceGroup in frameSourceGroups)
            {
                foreach (var sourceInfo in sourceGroup.SourceInfos)
                {
                    if (sourceInfo.MediaStreamType == MediaStreamType.VideoPreview
                        && sourceInfo.SourceKind == MediaFrameSourceKind.Color)
                    {

                        return (sourceGroup, sourceInfo);
                    }
                }
            }
            return (null, null);
        }

        private async Task<MediaCapture> InitializeMediaCapture(MediaFrameSourceGroup selectedGroup)
        {
            var mediaCapture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings()
            {
                SourceGroup = selectedGroup,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                StreamingCaptureMode = StreamingCaptureMode.Video
            };
            try
            {
                await mediaCapture.InitializeAsync(settings);
                return mediaCapture;
            }
            catch (Exception ex)
            {
                mediaCapture.Dispose();
                Debug.LogError(ex);
                return null;
            }
        }

        private async Task<bool> SetFormat(MediaFrameSource frameSource)
        {
            var supportedFormats = frameSource.SupportedFormats;
            var preferredFormat = supportedFormats.OrderBy(x => x.VideoFormat.Width).FirstOrDefault();

            if (preferredFormat == null)
            {
                return false;
            }

            await frameSource.SetFormatAsync(preferredFormat);
            return true;
        }

        private async void ColorFrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            var currentTime = m_Stopwatch.Elapsed;
            if (currentTime < m_LatestTime + TimeSpan.FromSeconds(captureInterval))
            {
                return;
            }

            m_LatestTime = currentTime;

            var mediaFrameReference = sender.TryAcquireLatestFrame();
            var coordinateSystem = mediaFrameReference?.CoordinateSystem;
            var videoMediaFrame = mediaFrameReference?.VideoMediaFrame;
            var softwareBitmap = videoMediaFrame?.SoftwareBitmap;
            var cameraIntrinsics = videoMediaFrame?.CameraIntrinsics;

            if (softwareBitmap == null)
            {
                return;
            }

            var imageWidth = softwareBitmap.PixelWidth;
            var imageHeight = softwareBitmap.PixelHeight;

            if (m_GrayBytes == null)
                m_GrayBytes = new byte[imageWidth * imageHeight];
            if (m_RGBBytes == null)
                m_RGBBytes = new byte[4 * imageWidth * imageHeight];

            softwareBitmap.CopyToBuffer(m_RGBBytes.AsBuffer());
            softwareBitmap.Dispose();

            int c = 0;
            for (int i = 0; i < m_RGBBytes.Length; i += 4)
            {
                m_GrayBytes[c++] = m_RGBBytes[i + 1];  // green of Bgra8
            }

            m_Context.Post(_ =>
            {
                // WorldOriginCoordinateSystem should be accessed in main thread
                var worldOrigin = WorldOriginCoordinateSystem;
                if (worldOrigin == null)
                {
                    return;
                }

                var matrix = coordinateSystem?.TryGetTransformTo(worldOrigin);

                Pose pose = default;
                if (matrix.HasValue)
                {
                    pose = ToUnityPose(matrix.Value);
                }

                m_CameraData = new CameraData()
                {
                    pixels = m_GrayBytes,
                    width = imageWidth,
                    height = imageHeight,
                    principalPoint = ToUnityVector(cameraIntrinsics.PrincipalPoint),
                    focalLength = ToUnityVector(cameraIntrinsics.FocalLength),
                    cameraPose = pose,
                    coordinateSystem = coordinateSystem,
                };

                m_FrameReceived = true;
            }, null);
        }

        private static Vector2 ToUnityVector(System.Numerics.Vector2 vector)
        {
            return new Vector2(vector.X, vector.Y);
        }

        private static Pose ToUnityPose(System.Numerics.Matrix4x4 matrix)
        {
            System.Numerics.Matrix4x4 newMatrix = matrix;

            // Platform coordinates are all right handed and unity uses left handed matrices. so we convert the matrix
            // from rhs-rhs to lhs-lhs 
            // Convert from right to left coordinate system
            newMatrix.M13 = -newMatrix.M13;
            newMatrix.M23 = -newMatrix.M23;
            newMatrix.M43 = -newMatrix.M43;

            newMatrix.M31 = -newMatrix.M31;
            newMatrix.M32 = -newMatrix.M32;
            newMatrix.M34 = -newMatrix.M34;

            System.Numerics.Matrix4x4.Decompose(newMatrix, out _, out var numericsRotation, out var numericsTranslation);
            var translation = new Vector3(numericsTranslation.X, numericsTranslation.Y, numericsTranslation.Z);
            var rotation = new Quaternion(numericsRotation.X, numericsRotation.Y, numericsRotation.Z, numericsRotation.W);

            return new Pose(translation, rotation);
        }
#endif
    }
}