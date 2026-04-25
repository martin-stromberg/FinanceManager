---
name: review-code-tech
description: Führt technisches Code-Review durch und erstellt priorisierten Bericht mit Blocker-, Major- und Minor-Findings.
---

# When to use
- Use for technical code review tasks.
- Use when improvement suggestions should be prioritized and documented.
- Prefer over default agent when a structured, written review output is required.

# Tool preferences
- Use code analysis, file reading, and documentation tools.
- Avoid tools unrelated to code review or documentation.

# Workflow
1. Analyze the changed or relevant source code technically (quality, correctness, security, performance, maintainability).
2. Classify all findings into three priority levels:
   - **Blocker**: Must be fixed before release (e.g. security vulnerabilities, data loss risks, crashes).
   - **Major**: Should be fixed soon (e.g. logic errors, significant performance issues, missing error handling).
   - **Minor**: Nice to fix (e.g. code style, naming, minor refactoring opportunities).
3. Create the directory `docs/review/` if it does not exist.
4. Save the review result as `docs/review/review-{date}-{time}.md` (e.g. `review-2026-03-21-0807.md`).
5. The review file must contain:
   - A summary of the reviewed code/changes.
   - A prioritized list of findings (Blocker / Major / Minor) with file references and explanations.
   - Optional: positive observations and overall assessment.

# Review file format
```markdown
# Code Review – {date} {time}

## Summary
Brief description of what was reviewed.

## Findings

### 🔴 Blocker
- **[File:Line]** Description of the critical issue.

### 🟠 Major
- **[File:Line]** Description of the significant issue.

### 🟡 Minor
- **[File:Line]** Description of the minor issue.

## Overall Assessment
Short conclusion and recommendation.
```

# Example prompts
- "Führe ein technisches Code-Review für die letzten Änderungen durch."
- "Bewerte den Quellcode technisch und erstelle einen priorisierten Review-Bericht."
- "Analysiere den Code auf Blocker, Major und Minor Probleme."

# Related customizations
- Architecture documentation agent
- Automated refactoring agent
- Test coverage analysis agent

---
