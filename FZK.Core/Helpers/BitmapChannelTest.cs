using System;

/// <summary>
/// 相机采集位图后，获取通道数并处理（比如转换为OpenCV Mat）
/// </summary>
/// <param name="cameraBitmap">相机采集的位图</param>
public void ProcessCameraBitmap(Bitmap cameraBitmap)
{
    try
    {
        // 1. 获取通道数
        int channelCount = BitmapChannelHelper.GetChannelCount(cameraBitmap);
        Console.WriteLine($"当前位图通道数：{channelCount}");

        // 2. 根据通道数做不同处理（你的视觉逻辑）
        switch (channelCount)
        {
            case 1:
                // 灰度图：直接做畸变矫正/边缘检测
                Console.WriteLine("处理灰度图，执行单通道算法");
                ProcessGrayscaleImage(cameraBitmap);
                break;
            case 3:
                // RGB图：先转灰度，再处理
                Console.WriteLine("处理RGB图，先转换为灰度图");
                Bitmap grayBitmap = ConvertToGrayscale(cameraBitmap);
                ProcessGrayscaleImage(grayBitmap);
                grayBitmap.Dispose(); // 释放资源
                break;
            case 4:
                // RGBA图：忽略Alpha通道，按RGB处理
                Console.WriteLine("处理RGBA图，忽略Alpha通道");
                Bitmap rgbBitmap = RemoveAlphaChannel(cameraBitmap);
                ProcessGrayscaleImage(rgbBitmap);
                rgbBitmap.Dispose();
                break;
        }
    }
    catch (NotSupportedException ex)
    {
        // 捕获不支持的格式，记录日志并提示
        Console.WriteLine($"图像格式错误：{ex.Message}");
        // 可在这里触发UI提示或降级处理
    }
    finally
    {
        // 释放位图资源（工业场景避免内存泄漏）
        cameraBitmap?.Dispose();
    }
}

// 辅助方法：RGB转灰度（示例）
private Bitmap ConvertToGrayscale(Bitmap rgbBitmap)
{
    Bitmap grayBitmap = new Bitmap(rgbBitmap.Width, rgbBitmap.Height, PixelFormat.Format8bppIndexed);
    // 此处省略灰度转换逻辑（可调用OpenCV的Cv2.CvtColor）
    return grayBitmap;
}

// 辅助方法：移除Alpha通道（示例）
private Bitmap RemoveAlphaChannel(Bitmap rgbaBitmap)
{
    Bitmap rgbBitmap = new Bitmap(rgbaBitmap.Width, rgbaBitmap.Height, PixelFormat.Format24bppRgb);
    // 此处省略移除Alpha通道的逻辑
    return rgbBitmap;
}

// 灰度图处理核心逻辑（你的畸变矫正/对位算法）
private void ProcessGrayscaleImage(Bitmap grayBitmap)
{
    // 调用OpenCV做畸变矫正等操作
    // Mat mat = BitmapToMat(grayBitmap);
    // Cv2.Undistort(mat, ...);
}