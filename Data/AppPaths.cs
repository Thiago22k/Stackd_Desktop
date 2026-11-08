namespace Stackd.Data;

public static class AppPaths
{
    public static string DataDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Stackd");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DatabaseFile => Path.Combine(DataDirectory, "stackd.db");

    public static string SecretsDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Stackd");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string SecretsFile => Path.Combine(SecretsDirectory, "secrets.json");
}
