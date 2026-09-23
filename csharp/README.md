# GreenTest: C#

The C# GreenTest for Ecology Computing. Same purpose as python's and js's: bootstrap the
language, generate a small static site, serve it locally, and verify it's being served
correctly - proof C# is ready to use on this machine, not just assumed to be.

## Setup

Debian-based Linux only (Debian, Ubuntu, WSL2 running either) - see
[`../ECOLOGY.md`](../ECOLOGY.md#bootstrapping-a-new-machine).

```bash
cd csharp
./bootstrap.sh
```

`bootstrap.sh`:

1. Installs basic tooling via `apt` (`git`, `curl`, `wget`, `python3`, `python3-pip`,
   `python3-venv`) plus `libicu` - a hard runtime dependency of .NET itself. The
   package name changes per Debian release (`libicu72` on bookworm, `libicu76` on
   trixie), so the script asks `apt-cache` for the one this release ships.
2. Installs the .NET SDK (LTS channel, 10+) into `~/.dotnet` with Microsoft's official
   `dotnet-install.sh` if it's missing or too old - Debian's own repos don't ship a
   .NET SDK. Adds `~/.dotnet` to PATH in `~/.bashrc`.
3. Sets up a venv with Jupyter at `csharp/.venv`, the same way python/js do.
4. Opens `greentest.ipynb`.

## The notebook

`greentest.ipynb` is the starting point, like every other language here. It runs on
the plain Python kernel, and the C#-specific work lives in `greentest.cs` - the notebook
calls it from a bash cell, the same way js's notebook shells out to `node`.

C# doesn't use a C# Jupyter kernel: Microsoft deprecated .NET Interactive (Polyglot
Notebooks) in 2026. Its recommended replacement is **file-based apps**, a .NET 10
feature: a single `.cs` file that runs with `dotnet run file.cs` (or directly via a
shebang line), no `.csproj` required. That's what `greentest.cs` is.

Once you've been through the notebook, `greentest.cs` also works as a fast smoke test:

```bash
./greentest.cs             # narrated, explains each step
./greentest.cs --quick     # same steps, minimal output
```

## What it actually does

Verified against [vanilla-compost](https://github.com/EcologyComputing/vanilla-compost):

0. Confirm the .NET SDK is installed, runs, and is new enough (10+).
1. Point at your `vanilla-compost` clone (`../../vanilla-compost` by default, override
   with `VANILLA_COMPOST`).
2. Confirm it's a real git clone with the files `greentest.cs` needs.
3. Leave a timestamped note in `greenTest-Message.md`, copied into
   `vanilla-compost/src/posts/`.
4. Generate `posts.html`. `vanilla-compost` has `generate_posts.py` and
   `generate_posts.js` but no C# equivalent, so the same title/date-extraction and
   template-insertion logic is ported to C# in `GreenTestCore/PostsGenerator.cs`,
   referenced from `greentest.cs` via `#:project`.
5. Serve `vanilla-compost/src` on `http://localhost:8080/` using
   `System.Net.HttpListener` from the standard library - no external packages.
6. Fetch `posts.html` back and verify it matches what was generated, and that the sample
   post shows up.
7. Clean up.

If it goes green, C# is bootstrapped and working end to end on this machine.

## Unit tests

`GreenTestCore.Tests/` covers the posts generator:

```bash
dotnet test GreenTestCore.Tests
```

## Requirements

- Debian-based Linux with `apt` and `sudo`
- A `vanilla-compost` clone as a sibling of this repo
- Everything else (.NET SDK 10+, `libicu`, Python, Jupyter) is installed by `bootstrap.sh`
