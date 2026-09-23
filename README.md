# greenTest

Clone this repo first on a new machine. It's the on-ramp for everything else Ecology Computing publishes.

GreenTests are extended READMEs: instead of just describing how to verify a language/toolchain is bootstrapped correctly, each one is a notebook (or equivalent) that actually does it - generates a small static site, serves it locally, and verifies the served output matches what was generated. If it goes green, that language is ready to use on this machine, not just assumed to be.

## Quickstart

Debian-based Linux only (Debian, Ubuntu, WSL2 running either, etc.) - see
[`ECOLOGY.md`](./ECOLOGY.md#bootstrapping-a-new-machine).

Install `git` and the GitHub CLI, log in to GitHub, and clone this repo:

```bash
sudo apt update && sudo apt install -y git gh && gh auth login && gh repo clone EcologyComputing/greenTest
```

Then:

1. Clone whichever other repos you're about to work on as siblings of this one, like `vanilla-compost` (`gh repo clone EcologyComputing/vanilla-compost`).
2. `cd greenTest/python` and run `./bootstrap.sh`, then its notebook - it's the prerequisite for every other language.
3. Pick any other language you need below, `cd` into it, and run its bootstrap script.
4. Read [`ECOLOGY.md`](./ECOLOGY.md) for the methodology this all sits inside - the guiding principles, the mapping approach, and the convention for working with this repo whether you're a person, an AI agent, or both.

## Starting a session

Opening prompt for a fresh CLI LLM session, once you've cloned this repo:

> Let's chat about what we're working on. See greenTest. Update your AGENTS.md as needed.

This works whether or not the tool auto-discovers `AGENTS.md` on its own since it explicitly points the session at this repo and asks it to keep the project's own `AGENTS.md` current as you go.

## Languages

| Language | Status | Verifies |
|---|---|---|
| Python | done | [vanilla-compost](https://github.com/EcologyComputing/vanilla-compost) |
| Node | done | [vanilla-compost](https://github.com/EcologyComputing/vanilla-compost) |
| Java | planned | TBD |
| C# | planned | TBD |
| Rust | planned | TBD |
| Go | planned | TBD |

Each one follows the same shape: `bootstrap.sh` gets the language's toolchain ready, then a notebook (or that language's closest equivalent) generates a tiny static site, serves it locally, and verifies the served output matches what was generated.

The notebook is the required starting point for everyone - that's where a language explains itself as it goes. Python has nothing else, since its notebook is the one everyone is assumed to have been through first. Other languages may *also* ship a plain script alongside the notebook (e.g. `js/greentest.js`) as a smoke test - for a developer who's already done the notebook once and just wants a fast, scriptable way to confirm the environment still works before they start coding. That script is never the recommended first stop.

Python's notebook is the prerequisite for the rest: it also covers one-time, per-machine setup (git identity, GitHub authentication) that other languages' notebooks don't repeat. Every other notebook follows python's flow from Step 1 on, and its own Step 0 only checks that language's toolchain. Each language's `bootstrap.sh` still installs python3 and Jupyter into a `.venv` inside its own folder rather than sharing one at the top level, so any language folder bootstraps, runs, and can be deleted on its own.

## Also here

- `ECOLOGY.md` - the methodology: guiding principles, mapping approach, how to work with this repo as a human, an agent, or both. `AGENTS.md` is a one-line pointer to it, kept only so tools that auto-discover that filename by convention still find their way here.