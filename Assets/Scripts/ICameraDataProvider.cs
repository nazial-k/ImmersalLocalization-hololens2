/*===============================================================================
Copyright (C) 2024 Immersal Ltd. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sdk@immersal.com for licensing requests.
===============================================================================*/

using UnityEngine;

public interface ICameraDataProvider
{
    bool TryAcquireLatestData(out byte[] pixels, out int width, out int height, out Vector4 intrinsics, out Pose cameraPose);
    void StartCapture();
    void StopCapture();
}