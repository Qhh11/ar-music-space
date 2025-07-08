using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class MANOPredictionRequest
{
    public string image;
}

[System.Serializable]
public class MANOPredictionResponse
{
    public bool success;
    public float[] vertices;
    public int[] faces;
    public int num_vertices;
    public int num_faces;
    public string message;
    public string error;

    public float[][] GetVerticesAsJaggedArray()
    {
        if (vertices == null || vertices.Length == 0)
            return null;

        float[][] result = new float[num_vertices][];
        for (int i = 0; i < num_vertices; i++)
        {
            result[i] = new float[3];
            result[i][0] = vertices[i * 3 + 0]; // x
            result[i][1] = vertices[i * 3 + 1]; // y
            result[i][2] = vertices[i * 3 + 2]; // z
        }
        return result;
    }

    public int[][] GetFacesAsJaggedArray()
    {
        if (faces == null || faces.Length == 0)
            return null;

        int[][] result = new int[num_faces][];
        for (int i = 0; i < num_faces; i++)
        {
            result[i] = new int[3];
            result[i][0] = faces[i * 3 + 0]; // vertex index 1
            result[i][1] = faces[i * 3 + 1]; // vertex index 2
            result[i][2] = faces[i * 3 + 2]; // vertex index 3
        }
        return result;
    }
}

[System.Serializable]
public class ServerHealthResponse
{
    public string status;
    public bool model_loaded;
    public string device;
}

public class Client : MonoBehaviour
{
    [Header("Server Configuration")]
    [SerializeField]
    private string serverURL = "https://localhost:5000";

    [SerializeField]
    private float requestTimeout = 30f;

    [Header("Camera and Display")]
    [SerializeField]
    private Camera targetCamera;

    [SerializeField]
    private Vector3 meshScale = Vector3.one;

    [SerializeField]
    private bool flipZ = true; // Flip Z axis if needed for camera space conversion

    [Header("Materials")]
    [SerializeField]
    private Material handMeshMaterial;

    [SerializeField]
    private bool showWireframe = false;

    [Header("Debug")]
    [SerializeField]
    private bool enableDebugLogs = true;

    [SerializeField]
    private bool saveDebugTextures = false;

    // Internal state
    private GameObject currentHandMesh;
    private bool isProcessing = false;
    private Texture2D processedTexture;

    // Events
    public System.Action<Mesh> OnMeshReceived;
    public System.Action<string> OnErrorOccurred;
    public System.Action<bool> OnServerHealthChanged;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Set default camera if not assigned
        if (targetCamera == null)
            targetCamera = Camera.main;

        // Create default material if not assigned
        if (handMeshMaterial == null)
            CreateDefaultMaterial();

        // Check server health on start
        StartCoroutine(CheckServerHealth());
    }

    void CreateDefaultMaterial()
    {
        handMeshMaterial = new Material(Shader.Find("Standard"));
        handMeshMaterial.color = new Color(1f, 0.8f, 0.6f, 1f); // Skin color
        handMeshMaterial.SetFloat("_Metallic", 0f);
        handMeshMaterial.SetFloat("_Glossiness", 0.3f);
    }

    #region Public API Methods

    /// <summary>
    /// Predict hand mesh from a texture (main method)
    /// </summary>
    public void PredictFromTexture(Texture2D inputTexture)
    {
        if (isProcessing)
        {
           // LogDebug("Already processing a request. Please wait.");
            return;
        }

        if (inputTexture == null)
        {
            LogError("Input texture is null");
            return;
        }

        StartCoroutine(ProcessTextureAndPredict(inputTexture));
    }

    public GameObject getHandmesh()
    {
        return currentHandMesh;
    }
    /// <summary>
    /// Predict hand mesh from camera feed
    /// </summary>
    public void PredictFromCamera()
    {
        if (targetCamera == null)
        {
            LogError("Target camera is not set");
            return;
        }

        StartCoroutine(CaptureFromCameraAndPredict());
    }

    /// <summary>
    /// Predict from a file path (for testing)
    /// </summary>
    public void PredictFromFile(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            LogError("Image path is empty");
            return;
        }

        StartCoroutine(LoadImageAndPredict(imagePath));
    }

    /// <summary>
    /// Clear current hand mesh
    /// </summary>
    public void ClearMesh()
    {
        if (currentHandMesh != null)
        {
            DestroyImmediate(currentHandMesh);
            currentHandMesh = null;
        }
    }

    /// <summary>
    /// Check server health
    /// </summary>
    public void CheckHealth()
    {
        StartCoroutine(CheckServerHealth());
    }

    #endregion

    #region Image Processing

    private Texture2D ResizeAndPadTexture(Texture2D original)
    {
        // Convert to readable texture if needed
        Texture2D readableTexture = MakeTextureReadable(original);

        int originalWidth = readableTexture.width;
        int originalHeight = readableTexture.height;
        int targetSize = 128;

        // Calculate scaling ratio (based on longer side)
        float scale = (float)targetSize / Mathf.Max(originalWidth, originalHeight);

        // Calculate new dimensions
        int newWidth = Mathf.RoundToInt(originalWidth * scale);
        int newHeight = Mathf.RoundToInt(originalHeight * scale);

        // Resize texture
        Texture2D resizedTexture = ResizeTexture(readableTexture, newWidth, newHeight);

        // Create padded result texture
        Texture2D paddedTexture = new Texture2D(targetSize, targetSize, TextureFormat.RGB24, false);
        Color[] paddedPixels = new Color[targetSize * targetSize];

        // Fill with black
        for (int i = 0; i < paddedPixels.Length; i++)
            paddedPixels[i] = Color.black;

        // Calculate center position
        int startX = (targetSize - newWidth) / 2;
        int startY = (targetSize - newHeight) / 2;

        // Copy resized image to center
        Color[] resizedPixels = resizedTexture.GetPixels();

        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                int sourceIndex = y * newWidth + x;
                int targetIndex = (startY + y) * targetSize + (startX + x);
                paddedPixels[targetIndex] = resizedPixels[sourceIndex];
            }
        }

        paddedTexture.SetPixels(paddedPixels);
        paddedTexture.Apply();

        // Clean up intermediate textures
        if (readableTexture != original)
            DestroyImmediate(readableTexture);
        DestroyImmediate(resizedTexture);

        //LogDebug(
        //    $"Resized image from {originalWidth}x{originalHeight} to {targetSize}x{targetSize} (content: {newWidth}x{newHeight})"
        //);

        return paddedTexture;
    }

    private Texture2D MakeTextureReadable(Texture2D texture)
    {
        if (texture.isReadable)
            return texture;

        // Create a temporary RenderTexture
        RenderTexture renderTex = RenderTexture.GetTemporary(
            texture.width,
            texture.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear
        );
        Graphics.Blit(texture, renderTex);

        // Create readable texture
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;

        Texture2D readableTexture = new Texture2D(texture.width, texture.height);
        readableTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableTexture.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);

        return readableTexture;
    }

    private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
    {
        Texture2D result = new Texture2D(newWidth, newHeight, source.format, false);
        Color[] resultPixels = new Color[newWidth * newHeight];

        float xRatio = (float)source.width / newWidth;
        float yRatio = (float)source.height / newHeight;

        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                int sourceX = Mathf.FloorToInt(x * xRatio);
                int sourceY = Mathf.FloorToInt(y * yRatio);

                sourceX = Mathf.Clamp(sourceX, 0, source.width - 1);
                sourceY = Mathf.Clamp(sourceY, 0, source.height - 1);

                resultPixels[y * newWidth + x] = source.GetPixel(sourceX, sourceY);
            }
        }

        result.SetPixels(resultPixels);
        result.Apply();

        return result;
    }

    private string TextureToBase64(Texture2D texture)
    {
        byte[] imageBytes = texture.EncodeToJPG(95);
        return System.Convert.ToBase64String(imageBytes);
    }

    #endregion

    #region Coroutines

    private IEnumerator ProcessTextureAndPredict(Texture2D inputTexture)
    {
        isProcessing = true;

        try
        {
            // Preprocess texture
            processedTexture = ResizeAndPadTexture(inputTexture);

            // Save debug texture if enabled
            if (saveDebugTextures)
            {
                byte[] debugBytes = processedTexture.EncodeToJPG();
                System.IO.File.WriteAllBytes(
                    $"debug_processed_{System.DateTime.Now.Ticks}.jpg",
                    debugBytes
                );
               // LogDebug("Saved debug processed texture");
            }

            // Convert to base64
            string base64Image = TextureToBase64(processedTexture);

            // Make prediction request
            yield return StartCoroutine(SendPredictionRequest(base64Image));
        }
        finally
        {
            isProcessing = false;

            // Clean up processed texture
            if (processedTexture != null)
            {
                DestroyImmediate(processedTexture);
                processedTexture = null;
            }
        }
    }

    private IEnumerator CaptureFromCameraAndPredict()
    {
        // Create a render texture to capture camera feed
        RenderTexture renderTexture = new RenderTexture(512, 512, 24);
        targetCamera.targetTexture = renderTexture;
        targetCamera.Render();

        // Read the render texture into a Texture2D
        RenderTexture.active = renderTexture;
        Texture2D capturedTexture = new Texture2D(512, 512, TextureFormat.RGB24, false);
        capturedTexture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
        capturedTexture.Apply();

        // Reset camera
        targetCamera.targetTexture = null;
        RenderTexture.active = null;

        // Process the captured texture
        yield return StartCoroutine(ProcessTextureAndPredict(capturedTexture));

        // Clean up
        DestroyImmediate(capturedTexture);
        renderTexture.Release();
    }

    private IEnumerator LoadImageAndPredict(string imagePath)
    {
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture("file://" + imagePath))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                LogError($"Failed to load image: {www.error}");
                yield break;
            }

            Texture2D loadedTexture = DownloadHandlerTexture.GetContent(www);
            yield return StartCoroutine(ProcessTextureAndPredict(loadedTexture));

            DestroyImmediate(loadedTexture);
        }
    }

    private IEnumerator SendPredictionRequest(string base64Image)
    {
        MANOPredictionRequest request = new MANOPredictionRequest { image = base64Image };

        string jsonRequest = JsonUtility.ToJson(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);

        using (UnityWebRequest www = new UnityWebRequest($"{serverURL}/predict", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = Mathf.RoundToInt(requestTimeout);

           // LogDebug("Sending prediction request to server " + $"{serverURL}/predict");
            float startTime = Time.time;

            yield return www.SendWebRequest();

            float inferenceTime = Time.time - startTime;

            if (www.result != UnityWebRequest.Result.Success)
            {
                string error = $"Prediction request failed: {www.error}";
                LogError(error);
                OnErrorOccurred?.Invoke(error);
                yield break;
            }

            try
            {
                MANOPredictionResponse response = JsonUtility.FromJson<MANOPredictionResponse>(
                    www.downloadHandler.text
                );

                if (response.success)
                {
                    CreateAndDisplayMesh(
                        response.GetVerticesAsJaggedArray(),
                        response.GetFacesAsJaggedArray()
                    );
                }
                else
                {
                    LogError($"Prediction failed: {response.error}");
                    OnErrorOccurred?.Invoke(response.error);
                }
            }
            catch (System.Exception e)
            {
                LogError($"Failed to parse server response: {e.Message}");
                OnErrorOccurred?.Invoke(e.Message);
            }
        }
    }

    private IEnumerator CheckServerHealth()
    {
        using (UnityWebRequest www = UnityWebRequest.Get($"{serverURL}/health"))
        {
            www.timeout = 5;
            yield return www.SendWebRequest();

            bool isHealthy = false;

            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    ServerHealthResponse health = JsonUtility.FromJson<ServerHealthResponse>(
                        www.downloadHandler.text
                    );
                    isHealthy = health.model_loaded;
                }
                catch (System.Exception e)
                {
                    LogError($"Failed to parse health response: {e.Message}");
                }
            }
            else
            {
                LogError($"Health check failed: {www.error}");
            }

            OnServerHealthChanged?.Invoke(isHealthy);
        }
    }

    #endregion

    #region Mesh Creation and Display

    private void CreateAndDisplayMesh(float[][] vertices, int[][] faces)
    {
        try
        {
            ClearMesh();

            if (targetCamera == null)
            {
                LogError("Target camera is null");
                return;
            }

            // 1. 修改坐标系转换：直接使用相机空间坐标（不再转换到世界空间）
            Vector3[] unityVertices = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = new Vector3(
                    vertices[i][0],
                    vertices[i][1],
                    vertices[i][2]
                );
                // print($"[MANOClient] {vertices[i][0]} {vertices[i][1]} {vertices[i][2]}");

                // 翻转Z轴（相机空间正Z → Unity相机空间负Z）
                if (flipZ) vertex.z = -vertex.z;
                // vertex.z += 0.5f;

                unityVertices[i] = vertex; // 直接使用相机空间坐标
            }

            // 2. 修正三角形缠绕顺序
            List<int> triangles = new List<int>();
            for (int i = 0; i < faces.Length; i++)
            {
                triangles.Add(faces[i][0]);
                triangles.Add(faces[i][2]);
                triangles.Add(faces[i][1]);
            }

            Mesh mesh = new Mesh();
            mesh.vertices = unityVertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            currentHandMesh = new GameObject("MANO Hand Mesh");
            MeshFilter meshFilter = currentHandMesh.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = currentHandMesh.AddComponent<MeshRenderer>();

            meshFilter.mesh = mesh;
            meshRenderer.material = handMeshMaterial;

            // 将网格设为相机的子物体
            currentHandMesh.transform.localPosition = Vector3.zero;
            currentHandMesh.transform.localRotation = Quaternion.identity;
            currentHandMesh.transform.localScale = meshScale;
            currentHandMesh.transform.SetParent(targetCamera.transform, false);

            // GameObject cube1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // cube1.transform.SetParent(targetCamera.transform, false);
            // cube1.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            // cube1.transform.localPosition = new Vector3(0.2f, 0.2f, 0.3f);

            // GameObject cube2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // cube2.transform.SetParent(targetCamera.transform, false);
            // cube2.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            // cube2.transform.localPosition = new Vector3(-0.2f, 0.2f, 0.3f);

            // GameObject cube3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // cube3.transform.SetParent(targetCamera.transform, false);
            // cube3.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            // cube3.transform.localPosition = new Vector3(0.2f, -0.2f, 0.3f);

            // GameObject cube4 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // cube4.transform.SetParent(targetCamera.transform, false);
            // cube4.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            // cube4.transform.localPosition = new Vector3(-0.2f, -0.2f, 0.3f);
            

            if (showWireframe)
                meshRenderer.material.SetFloat("_Wireframe", 1f);

            //LogDebug($"Created mesh with {vertices.Length} vertices");
            OnMeshReceived?.Invoke(mesh);
        }
        catch (System.Exception e)
        {
            LogError($"Failed to create mesh: {e.Message}");
            OnErrorOccurred?.Invoke(e.Message);
        }
    }

    #endregion

    #region Utility Methods

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[MANOClient] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[MANOClient] {message}");
    }

    void OnDestroy()
    {
        ClearMesh();

        if (processedTexture != null)
            DestroyImmediate(processedTexture);
    }

    #endregion

    #region Public Properties

    public bool IsProcessing => isProcessing;
    public GameObject CurrentHandMesh => currentHandMesh;
    public Camera TargetCamera => targetCamera;

    #endregion
}
