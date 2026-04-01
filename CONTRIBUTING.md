# Contributing to SchemaSmithyFree
First off, thank you for considering contributing to SchemaSmithyFree!

Contributions are what make community-driven projects thrive, and we welcome pull requests, bug reports, and feature suggestions.

# Ways to Contribute
* **Report bugs** — Open an issue with detailed steps to reproduce.
* **Suggest enhancements** — Share your ideas for improving SchemaSmithyFree in an issue.
* **Submit code** — Add features, fix bugs, or improve documentation.
* **Improve docs** — Our [documentation site](https://schemasmith.com/documentation/mssql/community/getting-started.html), in-repo `docs/` directory, and README are always evolving. Contributions are welcome.

# Development Setup
The tools in SchemaSmithyFree target:
* **.NET**: `net10.0`
* **IDEs**: Visual Studio 2026 or JetBrains Rider
* **Database**: Tested against SQL Server `2022-latest`. Should work with any database set to compatibility level 130 or higher.
### Running Locally with Docker
From the `demo/` directory:
```
cd demo
docker compose build
docker compose up
```
This spins up a SQL Server 2022 container on port **1450** and applies demo schema packages. You may also want to pull the [SchemaSmithDemos](https://github.com/Schema-Smith/SchemaSmithDemos) repository for additional demo products and examples.

Connection details are defined in `demo/.env`. Connect at `localhost:1450`.

# Coding Standards
* **Language**: C#
* **Testing**: Add or update unit/integration tests for any new functionality.
* **Database**: Ensure contributions work on supported SQL Server versions.

# Workflow
1. **Fork** the repository and create a feature branch:
   `git checkout -b feature/my-new-feature`
1. **Commit messages** should be concise and descriptive (e.g., `Fix state diff logic for dropped columns`).
1. **Push** to your fork and open a **pull request** against `main`.
1. Make sure:
  - A detailed issue is created documenting the change
  - CI tests pass
  - Changes align with project goals

# Pull Request Guidelines
* Keep PRs focused: one feature or fix per PR.
* Include tests and well documented issues.
* Reference related issues (e.g., `Fixes #42`).
* Be prepared to make changes requested during review.

# Community & Communication
* Be respectful and constructive in discussions.
* Assume positive intent.
* Remember we're all working to make SchemaSmithyFree better.

# License
By contributing, you agree that your contributions will be licensed under the [SSCL v2.0](LICENSE), the same license as the project.
