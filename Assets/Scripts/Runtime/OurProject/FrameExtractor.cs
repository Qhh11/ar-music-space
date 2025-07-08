using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;

public class FrameExtractor : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARCameraManager arCameraManager;

    [Header("Client Reference")]
    [SerializeField] private Client manoClient;

    [Header("Processing Settings")]
    [SerializeField][Range(0.1f, 5f)] private float processingInterval = 0.2f;
    [SerializeField] private bool processEveryFrame = false;

    // Internal state
    private float timeSinceLastProcess;
    private Texture2D currentFrameTexture;
    private XRCameraIntrinsics currentIntrinsics;

    void Start()
    {
        if (arCameraManager == null)
        {
            arCameraManager = FindObjectOfType<ARCameraManager>();
            if (arCameraManager == null)
            {
                Debug.LogError("ARCameraManager not found in the scene.");
                enabled = false;
                return;
            }
        }

        if (manoClient == null)
        {
            manoClient = FindObjectOfType<Client>();
            if (manoClient == null)
            {
                Debug.LogWarning("MANO Client not found in the scene. Hand prediction will be disabled.");
            }
        }

        arCameraManager.frameReceived += OnCameraFrameReceived;
        timeSinceLastProcess = processingInterval;
    }

    void OnDestroy()
    {
        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }

        if (currentFrameTexture != null)
        {
            Destroy(currentFrameTexture);
        }
    }

    void Update()
    {
        if (!processEveryFrame)
        {
            timeSinceLastProcess += Time.deltaTime;
        }
    }

    private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        if (!arCameraManager.TryGetIntrinsics(out currentIntrinsics))
        {
            Debug.LogWarning("Failed to get camera intrinsics.");
        }
        // else
        // {
        //     LogIntrinsics(currentIntrinsics);
        // }

        // if (args.projectionMatrix.HasValue)
        // {
        //     Debug.Log(GetProjectionMatrixString(args.projectionMatrix.Value));
        // }

        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            return;
        }

        bool shouldProcess = processEveryFrame || timeSinceLastProcess >= processingInterval;
        if (shouldProcess && manoClient != null && !manoClient.IsProcessing)
        {
            ConvertAndProcessImage(image);
            timeSinceLastProcess = 0f;
        }

        image.Dispose();
    }

    private void ConvertAndProcessImage(XRCpuImage image)
    {
        try
        {
            // 获取摄像头方向
            CameraFacingDirection facing = arCameraManager.currentFacingDirection;

            // 创建转换参数 - 保持原始尺寸
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(image.width, image.height), // 使用原始尺寸
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            int bufferSize = image.GetConvertedDataSize(conversionParams);
            NativeArray<byte> buffer = new NativeArray<byte>(bufferSize, Allocator.Temp);
            image.Convert(conversionParams, buffer);

            // 创建或重用纹理
            if (currentFrameTexture == null ||
                currentFrameTexture.width != conversionParams.outputDimensions.x ||
                currentFrameTexture.height != conversionParams.outputDimensions.y)
            {
                if (currentFrameTexture != null) Destroy(currentFrameTexture);
                currentFrameTexture = new Texture2D(
                    conversionParams.outputDimensions.x,
                    conversionParams.outputDimensions.y,
                    conversionParams.outputFormat,
                    false
                );
            }

            currentFrameTexture.LoadRawTextureData(buffer);
            currentFrameTexture.Apply();
            buffer.Dispose();

            // 确定旋转角度（始终旋转为竖屏）
            int rotationAngle = (facing == CameraFacingDirection.User) ? 270 : 90;

            // 旋转纹理使其竖立
            Texture2D rotatedTexture = RotateTexture(currentFrameTexture, rotationAngle);

            // 发送到Client处理
            manoClient.PredictFromTexture(rotatedTexture);

            // 销毁临时旋转纹理
            Destroy(rotatedTexture);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to convert AR image: {e.Message}");
        }
    }

    // 旋转纹理方法 - 修复尺寸问题
    private Texture2D RotateTexture(Texture2D originalTexture, int rotationAngle)
    {
        int width = originalTexture.width;
        int height = originalTexture.height;

        // 计算旋转后的尺寸
        int rotatedWidth = (rotationAngle % 180 == 0) ? width : height;
        int rotatedHeight = (rotationAngle % 180 == 0) ? height : width;

        // 创建新纹理
        Texture2D rotatedTexture = new Texture2D(
            rotatedWidth,
            rotatedHeight,
            originalTexture.format,
            false
        );

        // 获取原始像素
        Color32[] originalPixels = originalTexture.GetPixels32();
        Color32[] rotatedPixels = new Color32[rotatedWidth * rotatedHeight];

        // 旋转像素
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int newX, newY;
                int newIndex, oldIndex = y * width + x;

                switch (rotationAngle)
                {
                    case 90: // 顺时针90度
                        newX = height - y - 1;
                        newY = x;
                        newIndex = newY * rotatedWidth + newX;
                        rotatedPixels[newIndex] = originalPixels[oldIndex];
                        break;

                    case 180: // 180度
                        newX = width - x - 1;
                        newY = height - y - 1;
                        newIndex = newY * rotatedWidth + newX;
                        rotatedPixels[newIndex] = originalPixels[oldIndex];
                        break;

                    case 270: // 逆时针90度（顺时针270度）
                        newX = y;
                        newY = width - x - 1;
                        newIndex = newY * rotatedWidth + newX;
                        rotatedPixels[newIndex] = originalPixels[oldIndex];
                        break;

                    default: // 0度
                        newIndex = y * rotatedWidth + x;
                        rotatedPixels[newIndex] = originalPixels[oldIndex];
                        break;
                }
            }
        }

        rotatedTexture.SetPixels32(rotatedPixels);
        rotatedTexture.Apply();

        return rotatedTexture;
    }

    private void LogIntrinsics(XRCameraIntrinsics intrinsics)
    {
        Debug.Log($"Camera Intrinsics: \n" +
                  $"Resolution: {intrinsics.resolution}\n" +
                  $"Focal Length: ({intrinsics.focalLength.x:F3}, {intrinsics.focalLength.y:F3})\n" +
                  $"Principal Point: ({intrinsics.principalPoint.x:F3}, {intrinsics.principalPoint.y:F3})\n" +
                  $"Intrinsic Matrix:\n" +
                  $"{GetIntrinsicMatrixString(intrinsics)}");
    }

    private string GetIntrinsicMatrixString(XRCameraIntrinsics intrinsics)
    {
        return $"| {intrinsics.focalLength.x,7:F3}  {0,5}      {intrinsics.principalPoint.x,7:F3} |\n" +
               $"| {0,7}      {intrinsics.focalLength.y,5:F3}  {intrinsics.principalPoint.y,7:F3} |\n" +
               $"| {0,7}      {0,5}      {1,7:F3} |";
    }

    private string GetProjectionMatrixString(Matrix4x4 mat)
    {
        return $"| {mat[0, 0]} {mat[0, 1]} {mat[0, 2]} {mat[0, 3]} |\n" +
               $"| {mat[1, 0]} {mat[1, 1]} {mat[1, 2]} {mat[1, 3]} |\n" +
               $"| {mat[2, 0]} {mat[2, 1]} {mat[2, 2]} {mat[2, 3]} |\n" +
               $"| {mat[3, 0]} {mat[3, 1]} {mat[3, 2]} {mat[3, 3]} |\n";
    }
}