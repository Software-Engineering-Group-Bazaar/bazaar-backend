# Git Branching Overview

We will be using the following Git branches:

*   `main`: The production-ready branch. No one merges directly into `main` until professors/assistants approve. Acts as the stable release.
*   `develop`: The active development branch. All new features and bug fixes go here first.
*   `feature/*`: Used for new features or improvements. Each feature must be on a separate branch.
*   `bugfix/*`: For fixing bugs found during development. Merged into `develop`.
*   `hotfix/*`: For urgent fixes that go directly into `main` (only in critical situations, e.g., production bug).

# Naming Convention

Stick to the following naming patterns for consistency:

*   **Features:** `feature/documentation`, `feature/jwt-token-generation`, `feature/user-profile-page`
*   **Bug fixes:** `bugfix/null-pointer-auth`, `bugfix/invalid-request-header`
*   **Hotfixes:** `hotfix/login-crash-main`, `hotfix/cors-error-prod`

#  Workflow: Creating and Merging a Feature Branch

Follow these exact steps when creating a new feature.

1.  **Switch to `develop` and pull the latest changes**
    ```bash
    git checkout develop
    git pull origin develop
    ```

2.  **Create a new feature branch**
    ```bash
    git checkout -b feature/jwt-token-generation
    ```
    Replace `jwt-token-generation` with a short, descriptive name of your feature.

3.  **Work on your feature (add files, make changes)**
    Make the changes in your codebase. Then stage and commit them:
    ```bash
    git add .
    git commit -m "Closes #42 - Implemented JWT token generation"
    ```
    Replace `42` with the GitHub issue number if you're using issues. Start your commit message with a relevant verb (e.g., Feat, Fix, Chore, Docs).

4.  **Push your feature branch to GitHub**
    ```bash
    git push origin feature/jwt-token-generation
    ```

# Merging Your Feature into `develop`

Once your feature is finished and pushed, go to GitHub and open a Pull Request (PR) from your `feature/*` branch into the `develop` branch. After a teammate reviews and approves the code, follow these steps locally (or let GitHub handle the merge if preferred, ensuring "Create a merge commit" is selected):

1.  **Merge the feature into `develop`**
    ```bash
    git checkout develop
    git pull origin develop
    git merge --no-ff feature/jwt-token-generation -m "Merge feature/jwt-token-generation into develop"
    git push origin develop
    ```
    The `--no-ff` flag ensures the merge commit is retained in history for better traceability of features.

# Bug Fixes (Non-Critical)

When you find a bug during development (not in `main`):

1.  **Create a `bugfix` branch from `develop`**
    ```bash
    git checkout develop
    git pull origin develop
    git checkout -b bugfix/null-pointer-auth
    ```
    Replace `null-pointer-auth` with a short, descriptive name of the bug fix.

2.  **Fix the bug, commit and push**
    ```bash
    # Make your code changes to fix the bug
    git add .
    git commit -m "Fix: Handle null pointer in authentication service"
    git push origin bugfix/null-pointer-auth
    ```

3.  **Merge into `develop`**
    Open a Pull Request from `bugfix/*` to `develop`. After approval, merge:
    ```bash
    git checkout develop
    git pull origin develop
    git merge --no-ff bugfix/null-pointer-auth -m "Merge bugfix/null-pointer-auth into develop"
    git push origin develop
    ```

# Hotfixes (Critical - `main` Only)

Only apply hotfixes for critical bugs found in the `main` branch (production) and **only after consultation approval**. These go directly into `main` first, then are merged back to `develop`.

1.  **Create `hotfix` branch from `main`**
    ```bash
    git checkout main
    git pull origin main
    git checkout -b hotfix/login-crash-main
    ```
    Replace `login-crash-main` with a short, descriptive name of the critical fix.

2.  **Fix the bug, commit and push**
    ```bash
    # Make your code changes to fix the critical bug
    git add .
    git commit -m "Hotfix: Fix login crash issue"
    git push origin hotfix/login-crash-main
    ```

3.  **Merge hotfix into `main` AND `develop`**

    *Merge into `main`:*
    ```bash
    git checkout main
    git pull origin main
    git merge --no-ff hotfix/login-crash-main -m "Merge hotfix/login-crash-main into main"
    git push origin main
    ```
    *Merge into `develop` (to ensure the fix is also in future development):*
    ```bash
    git checkout develop
    git pull origin develop
    git merge --no-ff hotfix/login-crash-main -m "Merge hotfix/login-crash-main into develop"
    git push origin develop
    ```

#  Merging `develop` into `main` for Release

>**Do not perform this merge until your team gets explicit approval from the assistant or professional.**

When a release is approved and ready:

1.  **Merge `develop` into `main`**
    Ensure `develop` is stable and fully tested.
    ```bash
    git checkout main
    git pull origin main
    git merge --no-ff develop -m "Release version 1.0.0"
    git push origin main
    ```
    Replace `1.0.0` with the actual version number for the release.

2.  **Tag the release**
    Tagging marks a specific point in history as important, typically used for releases.
    ```bash
    git tag -a v1.0.0 -m "Version 1.0.0 release"
    git push origin v1.0.0
    ```
    Replace `v1.0.0` with your actual tag name (often prefixed with 'v').

# Summary Cheat Sheet

```bash
# === Create a new feature ===
git checkout develop
git pull origin develop
git checkout -b feature/your-feature-name
# ... work on files ...
git add .
git commit -m "Feat: Describe your change (Closes #issue_number)"
git push origin feature/your-feature-name
# -> Open PR on GitHub: feature/your-feature-name -> develop

# === Merge approved feature PR into develop (Locally) ===
git checkout develop
git pull origin develop
git merge --no-ff feature/your-feature-name -m "Merge feature/your-feature-name into develop"
git push origin develop
# (Optionally, delete the feature branch locally and remotely)
# git branch -d feature/your-feature-name
# git push origin --delete feature/your-feature-name

# === Create a non-critical bug fix ===
git checkout develop
git pull origin develop
git checkout -b bugfix/your-bugfix-name
# ... fix the bug ...
git add .
git commit -m "Fix: Describe bug fix (Fixes #issue_number)"
git push origin bugfix/your-bugfix-name
# -> Open PR on GitHub: bugfix/your-bugfix-name -> develop
# -> Merge PR (similar steps as feature merge)

# === Release: Merge develop into main (AFTER APPROVAL) ===
git checkout main
git pull origin main
git merge --no-ff develop -m "Release version x.x.x"
git push origin main
git tag -a vx.x.x -m "Version x.x.x release"
git push origin vx.x.x
