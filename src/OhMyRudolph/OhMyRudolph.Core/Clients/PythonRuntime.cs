namespace OhMyRudolph.Core.Clients;

public static class PythonRuntime
{
    public static string ResolveExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable("OHMYRUDOLPH_PYTHON");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!File.Exists(configured)) throw new FileNotFoundException("OHMYRUDOLPH_PYTHON does not point to an executable.");
            return configured;
        }
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, ".venv", "Scripts", "python.exe");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Python environment not found; create .venv or set OHMYRUDOLPH_PYTHON.");
    }
}
