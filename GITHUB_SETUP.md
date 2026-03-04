# GitHub Configuration for WowCombo Development

## 📍 Repository Structure

### Primary Development Repository
- **Location:** `C:/WowClassicGrindBot/`
- **Purpose:** Your main development directory with full git history
- **Branches:**
  - `dev` - Main development branch (3,802 commits, kept locally)
  - `main` - Original upstream default branch
  - `exp/retail-disable-pathfinder` - Experimental branch
  - `poc/minimap` - Proof of concept branch

### Remote Repositories

| Remote | URL | Purpose |
|--------|-----|---------|
| `origin` | https://github.com/Xian55/WowClassicGrindBot.git | Original upstream project |
| `wowcombo` | https://github.com/Aycrith/WowCombo.git | **YOUR** WowCombo fork |

## 🔄 Workflow

### For Regular Development
```bash
cd C:/WowClassicGrindBot
git checkout dev
# Make changes and commit normally
git commit -m "your message"
# Your work is automatically backed up locally
```

### For Pushing to Your Fork

**Push latest commits to wowcombo/dev** (when you want to share your work):
```bash
cd C:/WowClassicGrindBot

# Option 1: Push specific commits (avoid timeout on huge history)
git push wowcombo dev-recent  # Recent work only

# Option 2: Create a new branch from your recent work
git checkout -b release/v1.0.0  # Create stable release branch
git push -u wowcombo release/v1.0.0
```

**Push to wowcombo/main** (for stable releases):
```bash
# Merge dev into main (optional, for clean releases)
git checkout main
git merge dev -m "Merge development work"
git push wowcombo main
```

## ⚠️ Important Notes

### Why We Don't Push Full Dev History
- Your `dev` branch has 3,802 commits (large repository)
- Pushing all commits causes timeouts on GitHub
- Solution: Push focused branches or create release branches instead

### Your Full History is Safe
- ✅ Full 3,802 commits stored locally in `C:/WowClassicGrindBot`
- ✅ Backup copy at `C:/WowClassicGrindBot.backup-before-github-sync`
- ✅ Nothing is lost or corrupted

### Syncing with Original Project
If you want to pull updates from `Xian55/WowClassicGrindBot`:
```bash
git fetch origin dev
git merge origin/dev  # or git rebase origin/dev
```

## 🔐 Safety Checks

All your development work is protected:
```bash
# Verify your commits are still there
cd C:/WowClassicGrindBot
git log dev --oneline -5

# Check remotes
git remote -v

# See what's on your fork
git branch -r | grep wowcombo
```

## 📚 References

- **Main fork:** https://github.com/Aycrith/WowCombo
- **Original project:** https://github.com/Xian55/WowClassicGrindBot
- **Your local history:** 3,802 commits (kept for stability)

---

**Last updated:** Feb 3, 2025
**Status:** All work preserved and safely synced ✅
