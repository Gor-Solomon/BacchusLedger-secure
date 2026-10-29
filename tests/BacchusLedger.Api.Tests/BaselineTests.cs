namespace BacchusLedger.Api.Tests;

/// The same comparison as scripts/check.ps1, in process: status code and raw body of every endpoint in docs/refactor/baseline.
public class BaselineTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    static readonly string BaselineDir = Path.Combine(RepoRoot(), "docs", "refactor", "baseline");

    public static TheoryData<string, int, string> Endpoints()
    {
        var data = new TheoryData<string, int, string>();
        foreach (var line in File.ReadAllLines(Path.Combine(BaselineDir, "manifest.txt")))
            if (line.Split(' ', 3) is [var name, var status, var path] && name != "selftest") data.Add(name, int.Parse(status), path);
        return data;
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Response_is_identical_to_the_baseline(string name, int status, string path)
    {
        var resp = await factory.CreateClient().GetAsync(path);
        Assert.Equal(status, (int)resp.StatusCode);
        var expected = await File.ReadAllBytesAsync(Path.Combine(BaselineDir, name + ".json"));
        var actual = await resp.Content.ReadAsByteArrayAsync();
        Assert.Equal(System.Text.Encoding.UTF8.GetString(expected), System.Text.Encoding.UTF8.GetString(actual));
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "BacchusLedger.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("BacchusLedger.sln not found above " + AppContext.BaseDirectory);
    }
}
