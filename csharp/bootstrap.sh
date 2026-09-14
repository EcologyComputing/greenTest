#!/usr/bin/env bash
set -e

echo "GreenTest C# bootstrap"
echo "======================"

# Writes a PATH-related export line into the user's shell profile, once -
# skips it if that exact line is already there, so re-running this script
# never duplicates entries. This is what makes the PATH fix permanent
# instead of a one-off note the person has to remember to copy themselves.
_persist_to_profile () {
    LINE_TO_ADD="$1"
    PROFILE_FILE="${BASH_ENV:-$HOME/.bashrc}"
    [ -f "$HOME/.bashrc" ] && PROFILE_FILE="$HOME/.bashrc"
    [ -f "$HOME/.bash_profile" ] && [ ! -f "$HOME/.bashrc" ] && PROFILE_FILE="$HOME/.bash_profile"
    touch "$PROFILE_FILE" 2>/dev/null || return 1
    if ! grep -qF "$LINE_TO_ADD" "$PROFILE_FILE" 2>/dev/null; then
        echo "$LINE_TO_ADD" >> "$PROFILE_FILE"
        echo "  (added to $PROFILE_FILE)"
    else
        echo "  (already present in $PROFILE_FILE)"
    fi
}

# Unlike python/js, this bootstrap doesn't need to set up Jupyter. C#'s
# GreenTest is a file-based app (dotnet run greentest.cs) instead of a
# notebook - see csharp/README.md for why. That means the only real
# dependency here is the .NET SDK itself.

# 1. Check for an existing dotnet install, and whether it's new enough.
# File-based apps (`dotnet run app.cs` with no .csproj) require SDK 10.0.100
# or newer - that feature simply doesn't exist in older SDKs.
NEED_INSTALL=1
if which dotnet >/dev/null 2>&1; then
    FOUND_VERSION="$(dotnet --version 2>/dev/null || echo '')"
    FOUND_MAJOR="$(echo "$FOUND_VERSION" | cut -d. -f1)"
    if [ -n "$FOUND_MAJOR" ] && [ "$FOUND_MAJOR" -ge 10 ] 2>/dev/null; then
        echo "Found dotnet $FOUND_VERSION at $(which dotnet) - new enough."
        NEED_INSTALL=0
    else
        echo "Found dotnet $FOUND_VERSION at $(which dotnet), but file-based apps need SDK 10 or newer."
    fi
else
    echo "dotnet was not found on your system."
fi

# 2. Install .NET, if needed, using the official cross-platform install
# script rather than a system package manager. This is deliberate, not just
# a preference: distro packages of dotnet (apt/dnf/pacman) commonly lag
# behind, some distros don't ship .NET at all, and this repo has already had
# to route around exactly this kind of system-package friction once before
# (see python/bootstrap.sh's venv workaround for PEP 668). The install
# script works the same way on Debian, Fedora, Arch-based systems (including
# SteamOS), and anywhere else with bash + curl, installs into your own home
# directory, and needs no sudo and no system package manager at all.
if [ "$NEED_INSTALL" -eq 1 ]; then
    # dotnet-install.sh is a Linux/macOS-only script - its own --os flag
    # doesn't even accept a Windows value (only osx/macos/linux/linux-musl/
    # freebsd/rhel.6 are supported). Running it under Git Bash, MSYS2, or
    # Cygwin fails with an "OS name could not be detected" error, because
    # there's genuinely nothing for it to detect - not a bug to work around,
    # just the wrong tool for this OS. Detect that case up front and hand
    # back manual instructions instead, the same way python/js's
    # bootstrap.sh already do for Windows.
    IS_WINDOWS=0
    case "$(uname -s 2>/dev/null)" in
        MINGW*|MSYS*|CYGWIN*) IS_WINDOWS=1 ;;
    esac
    if [ "${OS:-}" = "Windows_NT" ]; then IS_WINDOWS=1; fi

    if [ "$IS_WINDOWS" -eq 1 ]; then
        echo "This looks like Git Bash, MSYS2, or Cygwin on Windows."
        echo "The official install script this uses on Linux/macOS (dotnet-install.sh) can't target Windows - so this uses the Windows sibling script (dotnet-install.ps1) via powershell.exe instead, which Git Bash can reach directly."

        _manual_instructions () {
            echo "Install .NET SDK 10 manually instead, then re-open this terminal and re-run this script:"
            echo "  winget install Microsoft.DotNet.SDK.10"
            echo "  or download the installer:  https://dotnet.microsoft.com/download/dotnet/10.0"
        }

        if ! command -v powershell.exe >/dev/null 2>&1; then
            echo "powershell.exe isn't reachable from this shell, so this can't be automated here."
            _manual_instructions
            exit 1
        fi

        echo "Installing .NET SDK (LTS channel) via dotnet-install.ps1 ..."
        # -ExecutionPolicy Bypass only applies to this one invocation, not a
        # system-wide policy change - it's the standard way a non-PowerShell
        # caller runs a .ps1 without requiring the policy already be loosened.
        if ! powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \
            "& ([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1'))) -Channel LTS"; then
            echo "Automated install failed (see any PowerShell output above)."
            _manual_instructions
            exit 1
        fi

        # No -InstallDir was given above, so dotnet-install.ps1 used its
        # Windows default: %LOCALAPPDATA%\Microsoft\dotnet. Translate that
        # into the POSIX-style path Git Bash needs to put it on PATH here.
        if command -v cygpath >/dev/null 2>&1; then
            DOTNET_WIN_DIR="$(cygpath -u "$LOCALAPPDATA")/Microsoft/dotnet"
        else
            DOTNET_WIN_DIR="$USERPROFILE/AppData/Local/Microsoft/dotnet"
        fi
        export PATH="$DOTNET_WIN_DIR:$PATH"

        echo "Adding this to your shell profile so dotnet is still on PATH in new Git Bash terminals:"
        echo "  export PATH=\"$DOTNET_WIN_DIR:\$PATH\""
        _persist_to_profile "export PATH=\"$DOTNET_WIN_DIR:\$PATH\""

    else
        if ! which curl >/dev/null 2>&1; then
            echo "curl is required to install .NET. Install it first:"
            echo "  macOS:   brew install curl"
            echo "  Linux:   your distro's package manager (e.g. sudo apt install curl, sudo pacman -S curl)"
            exit 1
        fi

        echo "Installing .NET SDK (LTS channel) into \$HOME/.dotnet ..."
        curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/greentest-dotnet-install.sh
        bash /tmp/greentest-dotnet-install.sh --channel LTS --install-dir "$HOME/.dotnet"
        rm -f /tmp/greentest-dotnet-install.sh

        export DOTNET_ROOT="$HOME/.dotnet"
        export PATH="$HOME/.dotnet:$PATH"

        echo "Adding these to your shell profile so dotnet is still on PATH in new terminals:"
        echo "  export DOTNET_ROOT=\"\$HOME/.dotnet\""
        echo "  export PATH=\"\$HOME/.dotnet:\$PATH\""
        _persist_to_profile "export DOTNET_ROOT=\"\$HOME/.dotnet\""
        _persist_to_profile "export PATH=\"\$HOME/.dotnet:\$PATH\""
    fi
fi

# On Alpine (musl, BusyBox), .NET needs a specific set of native libraries
# that a bare Alpine image simply doesn't ship - this isn't an edge case to
# reactively detect from a crash message the way libicu is on Debian
# (Debian's base images already have libgcc/libstdc++; Alpine's don't have
# any of this by default). Per Microsoft's own Alpine install docs, this
# set is unconditionally required, so install it proactively rather than
# waiting to see which specific missing library dotnet complains about.
if command -v apk >/dev/null 2>&1; then
    echo "Alpine detected - installing .NET's required native libraries via apk..."
    if [ "$(id -u)" -eq 0 ]; then
        apk add --no-cache icu-libs krb5-libs libgcc libintl libssl3 libstdc++ zlib
    else
        sudo apk add --no-cache icu-libs krb5-libs libgcc libintl libssl3 libstdc++ zlib
    fi
fi

if ! which dotnet >/dev/null 2>&1; then
    echo "dotnet install finished but 'dotnet' still isn't on PATH in this shell."
    if [ "${IS_WINDOWS:-0}" -eq 1 ]; then
        echo "Try: export PATH=\"\$LOCALAPPDATA/Microsoft/dotnet:\$PATH\" (translated via cygpath if needed)   then re-run this script."
    else
        echo "Try: export PATH=\"\$HOME/.dotnet:\$PATH\"   then re-run this script."
    fi
    exit 1
fi

# dotnet being on PATH doesn't mean it can actually run yet - on bare/minimal
# Debian-family images (e.g. a fresh `debian:bookworm` container), the .NET
# runtime itself fails immediately because libicu isn't installed. This is a
# real dependency of dotnet, not something specific to this GreenTest, and
# it's specifically a minimal-container gap - a real desktop Linux install
# already has libicu pulled in by something else. Detect it and fix it for
# real (install the missing library) rather than silently forcing
# DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1, which would "fix" this script but
# quietly break real culture/locale support for any other C# project run
# afterward - not a trade worth making silently.
DOTNET_CHECK_OUTPUT="$(dotnet --version 2>&1)" || true
if ! echo "$DOTNET_CHECK_OUTPUT" | grep -qE '^[0-9]+\.[0-9]'; then
    if echo "$DOTNET_CHECK_OUTPUT" | grep -qi "ICU package"; then
        echo ""
        echo "dotnet is installed, but this system is missing libicu - a real runtime dependency of .NET itself."
        if command -v apt-get >/dev/null 2>&1; then
            echo "Installing libicu72 via apt-get..."
            if [ "$(id -u)" -eq 0 ]; then
                apt-get update && apt-get install -y libicu72
            else
                sudo apt-get update && sudo apt-get install -y libicu72
            fi
        else
            echo "Install it with your system's package manager, then re-run this script:"
            echo "  Debian/Ubuntu:  sudo apt-get install -y libicu72   (or libicu-dev if that package name isn't found)"
            echo "  Alpine:         apk add icu-libs"
            echo "  Fedora/RHEL:    sudo dnf install libicu"
            echo "For just this GreenTest (not recommended for other C# projects, since it disables real culture/locale support), you can instead run with:"
            echo "  export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1"
            exit 1
        fi
    else
        echo "dotnet --version failed for an unexpected reason:"
        echo "$DOTNET_CHECK_OUTPUT"
        exit 1
    fi
fi
echo "Using $(dotnet --version) at $(which dotnet)"

# 3. Best-effort: compare against the latest known release (informational
# only, never blocks - mirrors the same check in python/js bootstrap.sh).
# No python dependency here, so this is done with plain grep instead of
# reaching for a python one-liner like the sibling scripts do.
LATEST=$(curl -s --max-time 5 "https://endoflife.date/api/dotnet.json" 2>/dev/null \
    | grep -o '"latest":"[^"]*"' | head -1 | cut -d'"' -f4) || LATEST=""
if [ -n "$LATEST" ]; then
    echo "Latest stable .NET release is $LATEST, for reference - not required."
fi

cd "$(dirname "$0")"

# 4. Make greentest.cs directly executable on Unix systems via its shebang
# line. Harmless to re-run; does nothing on Windows (chmod isn't meaningful
# there - use 'dotnet run greentest.cs' instead, see README).
chmod +x greentest.cs 2>/dev/null || true

# 5. Set up Jupyter for greentest.ipynb - same venv-based approach as
# python/js's bootstrap.sh (PEP 668 "externally-managed-environment" on
# modern Debian/Ubuntu blocks a bare `pip install` outside a venv, so this
# sidesteps that the same way they do, rather than requiring anyone change
# how their system python is set up). The notebook itself doesn't run any
# C#-specific Jupyter kernel - it uses the same plain Python kernel
# python/js's notebooks use, and calls into greentest.cs via bash cells,
# the same way js's notebook shells out to `node` rather than reimplementing
# JS-specific logic in Python. See README.md for why.
if ! which python3 >/dev/null 2>&1; then
    echo "python3 is required to run the Jupyter notebook (greentest.ipynb). Install it first:"
    echo "  macOS:   brew install python3"
    if command -v apk >/dev/null 2>&1; then
        echo "  Alpine:  apk add python3 py3-pip"
    else
        echo "  Linux:   sudo apt install python3 python3-pip python3-venv   (or your distro's equivalent)"
    fi
    echo "  Windows: https://www.python.org/downloads/"
    exit 1
fi

if ! which python >/dev/null 2>&1; then
    mkdir -p "$HOME/.local/bin"
    ln -sf "$(which python3)" "$HOME/.local/bin/python"
    export PATH="$HOME/.local/bin:$PATH"
fi

if [ ! -f ".venv/bin/activate" ] && [ ! -f ".venv/Scripts/activate" ]; then
    echo "Creating a virtual environment for Jupyter at csharp/.venv..."
    if ! python -m venv .venv; then
        rm -rf .venv
        echo "Could not create a virtual environment. On Debian/Ubuntu this usually means:"
        echo "  sudo apt install python3-venv"
        exit 1
    fi
fi

if [ -f ".venv/bin/activate" ]; then
    source ".venv/bin/activate"
else
    source ".venv/Scripts/activate"
fi

if ! python -m jupyter --version >/dev/null 2>&1; then
    echo "Jupyter not found. Installing it now (inside .venv)..."
    # --break-system-packages is safe here specifically: this pip install
    # only ever runs inside the .venv we just created above, never against
    # the real system Python, so there's nothing outside this disposable
    # folder for it to break. It's needed because some distros (confirmed
    # on Alpine) enforce PEP 668's "externally managed" check even inside
    # an activated venv, not just against the system interpreter directly.
    python -m pip install --quiet --break-system-packages notebook
fi
echo "Jupyter is ready."

echo ""
if [ "$NEED_INSTALL" -eq 1 ]; then
    echo "=========================================================================="
    echo "  dotnet was just installed - THIS TERMINAL doesn't see it yet."
    echo ""
    echo "  Open a NEW terminal window (or run: exec \$SHELL -l), THEN run:"
    echo "    python -m jupyter notebook greentest.ipynb"
    echo ""
    echo "  This isn't a bug or something specific to this script - every install-"
    echo "  your-own-tool script (nvm, rustup, pyenv, etc.) has this same limit:"
    echo "  a script can permanently update your shell PROFILE for future"
    echo "  terminals (done, above), but it can never reach back and change the"
    echo "  environment of the terminal that's already running it."
    echo "=========================================================================="
else
    echo "Bootstrap complete. Run the GreenTest with:"
    echo "  python -m jupyter notebook greentest.ipynb   (the notebook - start here)"
    if command -v busybox >/dev/null 2>&1; then
        echo "  dotnet run greentest.cs                       (fast, unnarrated)"
        echo ""
        echo "Note: this looks like a BusyBox system (e.g. Alpine). ./greentest.cs won't"
        echo "launch directly here - BusyBox's env doesn't support the -S flag its shebang"
        echo "line needs. Use 'dotnet run greentest.cs' instead (same workaround Windows"
        echo "already needs, for a different reason)."
    else
        echo "  ./greentest.cs                               (fast, unnarrated - Linux/macOS)"
        echo "  dotnet run greentest.cs                       (fast, unnarrated - any OS)"
    fi
fi