private void OnApplicationEarlyStart()
{
    string bepInExDir = Path.Combine(Application.dataPath, "..", "BepInEx");

    // Check if the BepInEx directory exists
    if (Directory.Exists(bepInExDir))
    {
        try
        {
            // Load assemblies here (replace with actual loading logic)
            // For example:
            // Assembly.LoadFrom(Path.Combine(bepInExDir, "SomeAssembly.dll"));
        }
        catch (Exception ex)
        {
            // Handle loading exceptions gracefully
            Debug.LogError("Failed to load BepInEx assemblies: " + ex.Message);
        }
    }
    else
    {
        Debug.LogWarning("BepInEx directory does not exist: " + bepInExDir);
    }
}