# Contributing to SchemaSmithyFree
First off, thank you for considering contributing to SchemaSmithyFree!

Contributions are what make open source projects thrive, and we welcome pull requests, bug reports, and feature suggestions.

# Ways to Contribute
* **Report bugs** — Open an issue with detailed steps to reproduce.
* **Suggest enhancements** — Share your ideas for improving SchemaSmithyFree in an issue.
* **Submit code** — Add features, fix bugs, or improve documentation.
* **Improve docs** — Our wiki and README are always evolving. Contributions are welcome.

# Development Setup
The tools in SchemaSmithyFree target:
* **.NET**: `net9.0`, `net481`
* **IDEs**: Visual Studio 2022 or JetBrains Rider
* **Database**: Tested against SQL Server `2019-CU27-ubuntu-20.04`. Should work with any database set to compatibility level 130 or higher.
Running Locally with Docker
From the project root:
```
docker compose build
docker compose up
```
This spins up a SQL Server 2019 container and applies the **Test Product** schema. You may also want to pull the [SchemaSmithDemos](https://github.com/Schema-Smith/SchemaSmithDemos) repository and run docker from there instead to apply several demo products for more test data and examples. 

Connection details are defined in `.env`. Connect at `localhost`.

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
* Remember we’re all working to make SchemaSmithyFree better.

# License
By contributing, you agree that your contributions will be licensed under the same license as the project.

