#!/usr/bin/env -S dotnet --
#:project ./GreenTestCore/GreenTestCore.csproj
// GreenTest for C# - Ecology Computing
//
// This is the required "notebook equivalent" for C#. Where python and JS
// ship a Jupyter notebook, C#'s notebook tooling (.NET Interactive /
// Polyglot Notebooks) was deprecated by Microsoft in 2026 - see README.md
// for the full story and links. This file is Microsoft's own recommended
// replacement: a "file-based app", an ordinary single C# file that runs
// directly with `dotnet run greentest.cs` (or `./greentest.cs` on
// Linux/macOS via the shebang line above), no .csproj required.
//
// The actual posts-generation logic (title/date extraction, sorting,
// template insertion) lives in GreenTestCore/PostsGenerator.cs, not here -
// it's referenced via the #:project directive above (a stable .NET 10
// file-based-app feature, not the still-preview #:include). That's what
// makes it possible to write real unit tests against it in
// GreenTestCore.Tests/, with an ordinary `dotnet test`, without giving up
// this file's own file-based, no-.csproj entry point.
//
// It walks through the same 7 steps as python/greentest.ipynb and
// js/greentest.ipynb, top to bottom, and is meant to be read like a
// notebook: run it, read what each step prints, then look at the code
// right below that step to see exactly what just happened.
//
// Run it narrated (default, for a first run) or fast (for repeat runs):
//   ./greentest.cs             explains each step as it goes
//   ./greentest.cs --quick     same steps, minimal output
//
// Steps 2 shells out to bash, the same split python's notebook makes with
// its %%bash cells - what it runs can be copy-pasted into a terminal and
// run on its own too, not just from inside this script.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using GreenTestCore;
using System.Threading;
using System.Threading.Tasks;

bool quick = args.Contains("--quick") || args.Contains("-q");

void Say(string narrated)
{
    if (!quick) Console.WriteLine(narrated);
}

Console.WriteLine("GreenTest C# runner");
Console.WriteLine("====================");

// ---------------------------------------------------------------------
// Step 0: environment check
// ---------------------------------------------------------------------
Say(@"
Step 0: Environment check
--------------------------
Confirming this is running on a .NET SDK new enough for file-based apps
(SDK 10+). If you haven't already, run bootstrap.sh in this same
directory first.");

Console.WriteLine($"Running on .NET {Environment.Version}.");

// ---------------------------------------------------------------------
// Step 1: point this script at your vanilla-compost clone
// ---------------------------------------------------------------------
Say(@"
Step 1: Point this script at your vanilla-compost clone
----------------------------------------------------------
Same assumption as python/js: vanilla-compost is cloned as a sibling of
this repo (../../vanilla-compost from here). Override with the
VANILLA_COMPOST environment variable if yours lives elsewhere.");

string vanillaCompost = Environment.GetEnvironmentVariable("VANILLA_COMPOST") ?? "../../vanilla-compost";
string vanillaCompostFull = Path.GetFullPath(vanillaCompost);
Console.WriteLine($"Testing vanilla-compost at: {vanillaCompost}");

// ---------------------------------------------------------------------
// Step 2: confirm the repo is cloned (via bash, like python's %%bash cell)
// ---------------------------------------------------------------------
Say(@"
Step 2: Confirm the repo is cloned
------------------------------------
This step runs in bash, not C# - the same split python's notebook makes
with a %%bash cell. It checks the files this script's own generator
needs below (html_template.html and the posts/ directory - there's no
generate_posts.cs in vanilla-compost to check for, since C# implements
that logic directly in this file), then confirms the folder is a real
git clone and not just a folder of files someone emailed you. Everything
in this block can be copied into a terminal and run as-is.");

string bashCheck = $@"
export VANILLA_COMPOST=""{vanillaCompost}""
echo ""Checking vanilla-compost at: $VANILLA_COMPOST""

if [ -f ""$VANILLA_COMPOST/README.md"" ] && [ -f ""$VANILLA_COMPOST/src/html_template.html"" ] && [ -d ""$VANILLA_COMPOST/src/posts"" ]; then
    echo ""Repo layout looks right (found README.md, src/html_template.html, src/posts/).""
else
    echo ""That path doesn't look like a vanilla-compost clone: $VANILLA_COMPOST""
    echo ""Clone it first: git clone https://github.com/EcologyComputing/vanilla-compost.git""
    exit 1
fi

if git -C ""$VANILLA_COMPOST"" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo ""Confirmed: this is a real git clone, not just a folder of files.""
    git -C ""$VANILLA_COMPOST"" remote get-url origin 2>/dev/null && echo ""Its 'origin' remote points there ^"" || echo ""No 'origin' remote set - fine if you haven't pushed anywhere yet.""
else
    echo ""Warning: no .git found there. Fine for a quick trial, but you won't be able to send changes back via pull request without a real clone.""
fi
";

int bashExit = RunBash(bashCheck);
if (bashExit != 0)
{
    Console.WriteLine("Aborting: vanilla-compost clone check failed (see above).");
    return 1;
}

// ---------------------------------------------------------------------
// Step 3: leave a note for this run
// ---------------------------------------------------------------------
Say(@"
Step 3: Leave a note for this run
-------------------------------------
Same as python/js: this gets appended to greenTest-Message.md (with a
timestamp), then copied into vanilla-compost's posts/ so it becomes part
of the site GeneratePostsHtml() is about to build below.");

string notes;
if (!Console.IsInputRedirected && !quick)
{
    Console.Write("Enter notes for this run (or press Enter for default): ");
    string? input = Console.ReadLine();
    notes = string.IsNullOrWhiteSpace(input) ? "Hello, World! (C# validation run)" : input.Trim();
}
else
{
    notes = "Hello, World! (C# automated validation run)";
}

string logPath = "greenTest-Message.md";
string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
File.AppendAllText(logPath, $"## {timestamp}\n\n{notes.Trim()}\n\n", Encoding.UTF8);
Console.WriteLine($"Notes appended to {logPath}");

string postsDir = Path.Combine(vanillaCompostFull, "src", "posts");
Directory.CreateDirectory(postsDir);
File.Copy(logPath, Path.Combine(postsDir, logPath), overwrite: true);
Console.WriteLine($"Copied {logPath} to {postsDir}/");

// ---------------------------------------------------------------------
// Step 4: generate posts.html
// ---------------------------------------------------------------------
Say(@"
Step 4: Generate posts.html
-------------------------------
vanilla-compost doesn't have a C# generator to shell out to (python has
generate_posts.py, js has generate_posts.js) - so this step is a direct
C# port of that same logic: extract a title/date from each markdown
post, sort newest first, and insert the list into html_template.html in
place of the <!-- posts--> marker.");

string srcDir = Path.Combine(vanillaCompostFull, "src");
string generated = PostsGenerator.GeneratePostsHtml(srcDir);

// ---------------------------------------------------------------------
// Step 5: serve the site locally
// ---------------------------------------------------------------------
Say(@"
Step 5: Serve the site locally
----------------------------------
Starts a small local HTTP server (built on System.Net.HttpListener from
the standard library, no external packages) at http://localhost:8080/,
serving vanilla-compost/src - the C# equivalent of python's
`python -m http.server` and js's server.js.");

using var cts = new CancellationTokenSource();
var (listener, serverTask) = StartServer(srcDir, 8080, cts.Token);
Console.WriteLine("Server started at http://localhost:8080/");
await Task.Delay(300); // give it a moment to start, same purpose as python's time.sleep(1)

// ---------------------------------------------------------------------
// Step 6: verify it's serving the update correctly
// ---------------------------------------------------------------------
Say(@"
Step 6: Verify it's serving the update correctly
-----------------------------------------------------
The actual 'green test': fetch posts.html from the server we just
started and check it matches what GeneratePostsHtml() wrote, and that
the sample post shows up.");

bool ok = true;
try
{
    using var http = new HttpClient();
    string served = await http.GetStringAsync("http://localhost:8080/posts.html");

    if (served != generated)
    {
        Console.WriteLine("FAILED: served posts.html doesn't match what GeneratePostsHtml() just wrote.");
        ok = false;
    }
    else if (!served.Contains("hello-compost"))
    {
        Console.WriteLine("FAILED: expected the hello-compost post to be listed.");
        ok = false;
    }
    else
    {
        Console.WriteLine("\u001b[32mGreen: the server is serving the freshly generated posts.html.\u001b[0m");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"FAILED: could not fetch from the local server: {ex.Message}");
    ok = false;
}

// ---------------------------------------------------------------------
// Step 7: clean up
// ---------------------------------------------------------------------
cts.Cancel();
listener.Stop();
listener.Close();
Console.WriteLine("Server stopped.");

if (!ok)
{
    return 1;
}

if (!quick)
{
    Console.WriteLine(@"
If everything above ran without errors, C# is bootstrapped and working
end to end on this machine, verified against a small app instead of just
assumed to be working. See greenTest-Message.md for a running history of
these runs.

See ../ECOLOGY.md for how this fits into the rest of the Ecology
Computing methodology.");
}

return 0;

// =======================================================================
// Local functions below - the posts-generation logic (title/date
// extraction, sorting, template insertion) now lives in
// GreenTestCore/PostsGenerator.cs instead of here, so it can be unit
// tested with a real xUnit project. What's left here is orchestration
// (bash checks, notes) and the small static file server.
// =======================================================================

static int RunBash(string script)
{
    var psi = new ProcessStartInfo
    {
        FileName = "bash",
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        UseShellExecute = false,
    };
    psi.ArgumentList.Add("-c");
    psi.ArgumentList.Add(script);

    using var proc = Process.Start(psi);
    proc!.WaitForExit();
    return proc.ExitCode;
}

static (HttpListener, Task) StartServer(string rootDir, int port, CancellationToken token)
{
    var listener = new HttpListener();
    listener.Prefixes.Add($"http://localhost:{port}/");
    listener.Start();

    string root = Path.GetFullPath(rootDir);

    Task task = Task.Run(async () =>
    {
        while (!token.IsCancellationRequested)
        {
            Task<HttpListenerContext> getContextTask;
            try
            {
                getContextTask = listener.GetContextAsync();
            }
            catch (Exception)
            {
                break; // listener was stopped
            }

            Task delayTask = Task.Delay(Timeout.Infinite, token);
            Task completed = await Task.WhenAny(getContextTask, delayTask);
            if (completed != getContextTask) break; // cancelled

            HttpListenerContext ctx;
            try
            {
                ctx = await getContextTask;
            }
            catch (Exception)
            {
                break;
            }

            _ = Task.Run(() => HandleRequest(ctx, root));
        }
    }, token);

    return (listener, task);
}

static void HandleRequest(HttpListenerContext ctx, string root)
{
    try
    {
        string urlPath = ctx.Request.Url?.AbsolutePath ?? "/";
        string relative = urlPath.TrimStart('/');
        string filePath = Path.GetFullPath(Path.Combine(root, relative == "" ? "index.html" : relative));

        // Path-traversal guard: the resolved path must still live inside
        // root - a URL like "/../../etc/passwd" must not escape it.
        if (filePath != root && !filePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            ctx.Response.StatusCode = 403;
            WriteText(ctx, "403 Forbidden");
            return;
        }

        if (Directory.Exists(filePath))
        {
            filePath = Path.Combine(filePath, "index.html");
        }

        if (!File.Exists(filePath))
        {
            ctx.Response.StatusCode = 404;
            WriteText(ctx, "404 Not Found");
            return;
        }

        byte[] bytes = File.ReadAllBytes(filePath);
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = MimeType(filePath);
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
    }
    catch (Exception ex)
    {
        try
        {
            ctx.Response.StatusCode = 500;
            WriteText(ctx, $"500 Internal Server Error: {ex.Message}");
        }
        catch
        {
            // response stream may already be closed; nothing more to do
        }
    }
    finally
    {
        ctx.Response.OutputStream.Close();
    }
}

static void WriteText(HttpListenerContext ctx, string text)
{
    byte[] bytes = Encoding.UTF8.GetBytes(text);
    ctx.Response.ContentType = "text/plain";
    ctx.Response.ContentLength64 = bytes.Length;
    ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
}

static string MimeType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
{
    ".html" => "text/html",
    ".css" => "text/css",
    ".js" => "text/javascript",
    ".jpg" or ".jpeg" => "image/jpeg",
    ".png" => "image/png",
    ".md" => "text/markdown",
    _ => "application/octet-stream",
};