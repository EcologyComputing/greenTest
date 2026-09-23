#!/usr/bin/env bash
set -e

echo "Vanilla Compost GreenTest C# bootstrap"
echo "======================================"

# 0. Install basic tooling, plus libicu - a hard runtime dependency of .NET
# itself (without it even `dotnet --version` crashes). Debian renames the
# package with each ICU release (libicu72 on bookworm, libicu76 on trixie,
# ...), so ask apt which one this release ships instead of hardcoding it.
sudo apt update
LIBICU=$(apt-cache search --names-only '^libicu[0-9]+$' | cut -d' ' -f1 | sort -V | tail -n1)
if [ -z "$LIBICU" ]; then
    echo "Couldn't find a libicu package via apt-cache. Check your apt sources, then re-run this script."
    exit 1
fi
sudo apt install git curl wget python3 python3-pip python3-venv "$LIBICU"

# Persists a directory on PATH for future shells (appends to ~/.bashrc,
# only if not already there) and exports it now so the rest of *this* script
# sees it immediately. Doesn't source ~/.bashrc - Debian's default one
# returns early for non-interactive shells (which is what this script is),
# so sourcing it here would be a no-op, not a real update.
ensure_path() {
    local dir="$1"
    case ":$PATH:" in
        *":$dir:"*) ;;
        *) export PATH="$dir:$PATH" ;;
    esac
    if ! grep -qxF "export PATH=\"$dir:\$PATH\"" "$HOME/.bashrc" 2>/dev/null; then
        echo "export PATH=\"$dir:\$PATH\"" >> "$HOME/.bashrc"
        echo "Added $dir to PATH in ~/.bashrc for future shells."
    fi
}

# 1. Check for .NET SDK 10+ - install it via Microsoft's official
# dotnet-install.sh (into ~/.dotnet, no sudo) if missing or too old.
# File-based apps (`dotnet run greentest.cs`, no .csproj) need SDK 10+, and
# Debian's own apt repos don't ship a .NET SDK at all.
export DOTNET_ROOT="$HOME/.dotnet"
[ -x "$DOTNET_ROOT/dotnet" ] && ensure_path "$DOTNET_ROOT"

dotnet_major() {
    dotnet --version 2>/dev/null | cut -d. -f1
}

DOTNET_MAJOR=$(dotnet_major)
if [ -z "$DOTNET_MAJOR" ] || [ "$DOTNET_MAJOR" -lt 10 ]; then
    echo ".NET SDK 10+ not found - installing via dotnet-install.sh into $DOTNET_ROOT..."
    curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel LTS --install-dir "$DOTNET_ROOT"
    ensure_path "$DOTNET_ROOT"
    if ! grep -qxF "export DOTNET_ROOT=\"\$HOME/.dotnet\"" "$HOME/.bashrc" 2>/dev/null; then
        echo "export DOTNET_ROOT=\"\$HOME/.dotnet\"" >> "$HOME/.bashrc"
        echo "Added DOTNET_ROOT to ~/.bashrc for future shells."
    fi
fi

if ! dotnet --version >/dev/null 2>&1; then
    echo "dotnet is installed but won't run:"
    dotnet --version || true
    exit 1
fi
if [ "$(dotnet_major)" -lt 10 ]; then
    echo "This needs .NET SDK 10 or newer, found $(dotnet --version). Upgrade it, then re-run this script."
    exit 1
fi
echo "Found .NET SDK $(dotnet --version) at $(which dotnet)"

# 2. Best-effort: compare against the latest known release (informational only,
# never blocks - if there's no network or the API is unreachable, just skip it)
LATEST=$(curl -s --max-time 5 "https://endoflife.date/api/dotnet.json" 2>/dev/null \
    | python3 -c "import json,sys
try:
    print(json.load(sys.stdin)[0]['latest'])
except Exception:
    pass" 2>/dev/null) || LATEST=""
if [ -n "$LATEST" ]; then
    echo "Latest stable .NET release is $LATEST, for reference - not required."
fi

# 3. Check python3 is installed (needed to host the Jupyter Notebook)
if ! which python3 >/dev/null 2>&1; then
    echo "python3 is required to run the Jupyter Notebook. Install it first:"
    echo "  sudo apt install python3 python3-pip python3-venv"
    exit 1
fi
echo "Found $(python3 --version) at $(which python3)"

# 4. If there's no bare 'python' command, symlink it to the verified python3.
if ! which python >/dev/null 2>&1; then
    mkdir -p "$HOME/.local/bin"
    ln -sf "$(which python3)" "$HOME/.local/bin/python"
    echo "No 'python' command found - symlinked it to your verified python3 at $HOME/.local/bin/python"
    ensure_path "$HOME/.local/bin"
fi

# 5. Check for pip
if ! python -m pip --version >/dev/null 2>&1; then
    echo "pip was not found for python. Try: python -m ensurepip --upgrade"
    exit 1
fi

cd "$(dirname "$0")"

# 6. greentest.cs runs directly via its shebang line - the notebook calls it
# that way, and so can you from a terminal for a quick smoke test.
chmod +x greentest.cs

# 7. Set up Python venv for Jupyter (a venv sidesteps Debian's PEP 668
# "externally-managed-environment" block on system-wide pip installs)
if [ ! -f ".venv/bin/activate" ]; then
    echo "Creating a virtual environment for Jupyter at csharp/.venv..."
    if ! python -m venv .venv; then
        rm -rf .venv
        echo "Could not create a virtual environment. Install python3-venv:"
        echo "  sudo apt install python3-venv"
        exit 1
    fi
fi

source ".venv/bin/activate"

# 8. Install Jupyter inside venv
if ! python -m jupyter --version >/dev/null 2>&1; then
    echo "Jupyter not found. Installing it now (inside .venv)..."
    python -m pip install --quiet notebook
fi
echo "Jupyter is ready."

# 9. Launch the GreenTest notebook. It inherits this script's PATH, so a
# freshly installed dotnet is visible to it without opening a new terminal.
echo "Starting greentest.ipynb..."
python -m jupyter notebook greentest.ipynb
