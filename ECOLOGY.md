# ECOLOGY.md

A convention for working on Ecology Computing projects for humans, AI agents, or some mix of both. Point any coding agent at this file before starting work. A human picking this up cold should read it too.

None of this is a personal methodology. It's a small, sensible set of defaults assembled from decades of practice across the software, ops, and mapping communities.

## Guiding Principles

1. **Minimal by design** - nothing goes into a tool, a process, or a repo unless it needs to be there. Prefer the standard library over a dependency, a markdown file over a database, a shell script over a framework.
2. **Bash after pencil and paper** - Humans should sketch a plan on pencil and paper (or a whiteboard) before writing any code, and the first code in any project should be bash. Agents can start in markdown before bash. Much like pencil and paper, markdown and bash should work as expected without any complications.
3. **Map a strategy** - Start with current state before mapping future state. Use black to map current state, red to highlight impediments, and blue to note remediating factors.
4. **Track progress** - Future state maps should show trajectories from current state positions to desired destinations. Kanban is the default method of tracking project work once it starts.  
5. **Use git** - Artifacts we intend to keep are kept in version control. In addition to application code this includes: maps, diagrams, strategies, plans, and notes. If it isn't tied to a repo then it probably doesn't count.

## Mapping methodology

In order:
1. Separate the problem/solution into sets of 3 or more using a tool like rugged baselines, social practice theory, or another triad. This provides items to re-assemble onto a wardley map.
2. Recenter it on a top-level Wardley map. See where the pieces sit on the evolution axis (genesis to commodity) before deciding what to build vs. buy vs. borrow.
3. Establish horizontal flow with methods like SIPOC analysis and value stream maps. Trace supply to customer, not just an org chart.
4. Wardley map the dependencies for each value stream process as needed. These scope to what a specific process depends on.

The value stream maps this produces are laid out in three bands: information flow at the top, artifact flow in the middle (the stages an artifact moves through), and a timing diagram at the bottom with lead time and wait time.

Source maps are draw.io XML, exported to PNG or SVG for embedding. See the `maps/` repo.

## Bootstrapping a new machine

Debian-based Linux only (Debian, Ubuntu, WSL2 running either, etc.) - every
`bootstrap.sh` and setup script in this ecosystem assumes `apt` and targets
that platform exclusively. Nothing here is tested or maintained for macOS or
Windows outside WSL.

1. Confirm `git` is available (usually preinstalled, or one prompt/package-manager command away).
2. Clone this repo (`greenTest`) - it's the one that explains everything else.
3. Clone whichever other repos the work in front of you needs, as siblings of this one.
4. Run `python/`'s `bootstrap.sh` and GreenTest notebook first - besides python, it sets up git identity and GitHub authentication for this machine.
5. For each other language/toolchain you need, `cd` into that language's folder here and run its `bootstrap.sh`, then its GreenTest notebook. A green result means that language works as expected on this machine, verified against vanilla compost. See `README.md` for which languages exist so far.

## Working with agents

- This file is what any AI coding agent should read before touching a repo in this ecosystem (or a tool-specific copy of it, like a project's own `LLM.md`). A thin `AGENTS.md` sits at the root of this repo purely so tools that auto-discover that filename by convention still find their way here.
- AI belongs on the mapping/thinking side of the work (steps 1-2 above), not as a shortcut past it. Agents can help with reading existing maps and describing what they show, drafting a first-pass map for a human to review, or helping separate a problem before it's mapped, among other tasks. Automation and code come after a map exists and has been reviewed, whether the one holding the keyboard is a person or a model.
- For unattended/autonomous agent loops against a well-defined batch of tasks, see `ralph-loop-template/` in this repo. It's a scaffold, not a replacement for this file. The loop still needs a map and a scoped task list before it starts.
- Multi-agent pairing sessions coordinate through `~/.ecology/`. The top-level `session.md` indexes per-project directories (`.ecology/$PROJECT/`); each has its own `session.md` rollup plus one session log per agent. Read the project's `session.md` and every session log there before starting work on that project - that's where prior decisions, scope changes, and open blockers are logged, not just in the project's own `spec.md`. Only ever write to your own session log; never edit another agent's file or merge them into one - that's how concurrent agents avoid clobbering each other's notes.
- Session log naming defaults to `$provider-session.md` (e.g. `claude-session.md`, `gemini-session.md`) - "claude" covers whichever underlying model is actually running (Sonnet, Opus, Fable, ...) unless that distinction starts mattering. Only split into `$provider-$model-session.md` (e.g. `claude-fable-session.md`, `claude-sonnet-session.md`) when you actually need to tell models from the same provider apart - for instance, running two of them side by side on comparable work to compare output. Don't split preemptively.
- `~/.ecology/envs/<workstation-label>.json` records each workstation's hardware and which agents are available/orchestrating there - see `AI/AI_tuis.ipynb`. Don't infer a workstation's identity from its hostname; it isn't reliable.

## Related repos

| Repo | What it is |
|---|---|
| `vanilla-compost` | The static-site template other sites are built on; also the flagship GreenTest demo app |
