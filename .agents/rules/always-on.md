# Always-On Project Rules

Use `AGENTS.md` as the canonical source of truth for this repository.

Always follow these constraints:

- Maintain the 3-layer ASP.NET Core MVC architecture (MVC, ServiceLayer, DataAccessLayer).
- Apply production-grade SDLC, security, testing, and code-quality expectations.
- Ask blocking questions before implementation when product intent is unclear.
- Explain trade-offs when proposing or evaluating an implementation approach.
- Do not hardcode secrets, credentials, tokens, API keys, PII, or production connection strings.
- Do not initialize Git or add `.gitignore` unless the user explicitly requests it.

