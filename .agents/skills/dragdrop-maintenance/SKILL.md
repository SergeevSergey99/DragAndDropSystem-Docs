---
name: dragdrop-maintenance
description: Instructions for maintaining and updating Claude skills when the DragAndDrop system codebase changes. Use this skill when you notice skills are outdated or when implementing significant architectural changes that require documentation updates.
---

# DragAndDrop System - Skills Maintenance Guide

**Purpose**: Keep Claude skills synchronized with codebase changes

**Last Updated**: 2026-08-13

**When to use this skill**:
- After implementing major architectural changes
- When adding/removing core components
- When refactoring existing systems
- When skills contain outdated information
- Periodically (every 2-3 months) for verification

---

## Current Skills Structure

**IMPORTANT CHANGE (2026-06-13)**: Core docs must reflect the policy-driven JIT transfer
architecture. Materialized planning, planner/executor split, batch `Atomic`, and resolver
hierarchies are no longer current.

### Structure

```
.claude/skills/
├── dragdrop-system/
│   ├── SKILL.md                    ← Quick reference (250 lines)
│   ├── CORE_CONCEPTS.md            ← Detailed concepts
│   ├── OPERATIONS.md               ← Operation flows
│   ├── ADVANCED_FEATURES.md        ← Advanced topics
│   └── EXAMPLES.md                 ← Demo breakdowns
├── dragdrop-architecture/
│   ├── SKILL.md                    ← Quick reference (300 lines)
│   ├── COMPONENTS.md               ← Component details
│   ├── STRATEGIES.md               ← Strategy deep dive
│   ├── DATA_FLOW.md                ← Flow diagrams
│   └── PERFORMANCE.md              ← Performance analysis
├── dragdrop-expert/
│   ├── SKILL.md                    ← Quick reference (310 lines)
│   ├── ANTIPATTERNS.md             ← Anti-pattern catalog
│   ├── BEST_PRACTICES.md           ← Extensions & optimizations
│   └── TESTING.md                  ← Test scenarios
└── dragdrop-maintenance/
    └── SKILL.md                    ← This file
```

### Benefits

✅ **Fast Loading**: SKILL.md files are compact (~250-310 lines)
✅ **On-Demand Details**: Claude reads detailed files only when needed
✅ **Better Organization**: Easier to find specific information
✅ **Maintainability**: Update specific sections without touching everything

---

## Skills Overview

### Current Skills

1. **dragdrop-system** - Quick usage reference
   - **SKILL.md**: Core architecture, essential concepts, quick patterns
   - **CORE_CONCEPTS.md**: DragContext, rules, transfer pipeline, DataBinding deep dive
   - **OPERATIONS.md**: Detailed flow diagrams for all operations
   - **ADVANCED_FEATURES.md**: Input routing, occupied-slot handling, tooltips, extensions
   - **EXAMPLES.md**: Complete demo scene breakdowns

2. **dragdrop-architecture** - Technical reference
   - **SKILL.md**: Design philosophy, component overview, quick reference
   - **COMPONENTS.md**: Full technical docs for all components
   - **STRATEGIES.md**: Strategy pattern implementation details
   - **DATA_FLOW.md**: Operation pipelines and data flow diagrams
   - **PERFORMANCE.md**: Hot paths, optimizations, benchmarks

3. **dragdrop-expert** - Expert guidance
   - **SKILL.md**: Architecture review checklist, decision rules, code review checklist
   - **ANTIPATTERNS.md**: Complete anti-pattern catalog
   - **BEST_PRACTICES.md**: Extension examples, optimizations
   - **TESTING.md**: Comprehensive test scenarios

4. **dragdrop-maintenance** - This skill
   - How to update modular skills structure
   - Change detection procedures
   - Verification checklist

---

## When Skills Need Updating

### Signs of Outdated Skills

🚩 **Immediate Update Required**:
- New core component added (e.g., `InventoryAcceptanceRequest`, `TransferItemConversionUtility`)
- Existing component refactored (e.g., `BaseSlot`/`ISlot` boundary changes, strategy capability split)
- New public API methods added
- Breaking changes to existing APIs
- New design patterns introduced

⚠️ **Update Soon**:
- New optional features added
- New examples/demos created
- Performance optimizations implemented
- New best practices discovered
- Common pitfalls identified

ℹ️ **Minor Update**:
- Bug fixes that don't change API
- Internal refactorings
- Comment/documentation improvements
- Example code improvements

---

## Update Procedure

### Step 1: Identify What Changed

**Run these checks**:

```bash
# Check recent commits
git log --oneline --since="3 months ago" -- Scripts/

# Find new files
git diff --name-status HEAD~20 HEAD -- Scripts/

# Search for new public classes
find Scripts/ -name "*.cs" -newer .claude/skills/dragdrop-system/SKILL.md

# Check for interface/class changes
grep -r "public.*interface\|public.*class" Scripts/Core/ Scripts/Inventories/ Scripts/Slots/
```

**Key files to monitor**:
- `Scripts/DragAndDropManager.cs` - Core orchestrator
- `Scripts/Core/Models/DragContext.cs` - State management
- `Scripts/Inventories/UniversalInventory.cs` - Main inventory
- `Scripts/Inventories/Strategies/` - Strategy implementations
- `Scripts/Inventories/InventoryAcceptanceRequest.cs` - Context-aware preview request
- `Scripts/Inventories/TransferItemConversionUtility.cs` - Boundary conversion and stack slicing
- `Scripts/Inventories/TransferConversionSession.cs` - Drag-scoped conversion identity
- `Scripts/Inventories/DropVerdict.cs` - Drop decision consumed by feedback visuals
- `Scripts/Inventories/DropPreviewController.cs` - Preview footprint and active verdict
- `Scripts/Rules/RuleEvaluationService.cs` - Adapter-domain boundary for rule validation
- `Scripts/Inventories/InventoryTransferEngine.cs` - JIT execution, rollback, swap, and event dispatch
- `Scripts/Inventories/IPlacementInventory.cs` - Topology-neutral placement contract
- `Scripts/Inventories/Strategies/IStrategy.cs` - Explicit and automatic candidate contract
- `Scripts/Inventories/InventoryRuntimeCapabilities.cs` - Runtime capabilities such as dynamic slot lifecycle
- `Scripts/DataBinding/InventoryDataBindingBase.cs` - DataBinding base (direct notifications, sync, conversion)
- `Scripts/DataBinding/ListInventoryDataBinding.cs` - Template for list-based DataBindings
- `Scripts/DataBinding/MappedSlotInventoryDataBinding.cs` - Template for slot-mapped DataBindings
- `Scripts/Slots/BaseSlot.cs` - Slot base class
- `Scripts/UI/FreeFormSlotLayout.cs` - Free-form slot positioning; layout only, no item mutation
- `Scripts/Rules/` - Rule system files

---

### Step 2: Update dragdrop-system Skill

**SKILL.md (Quick Reference)**:
- Update component overview if new components added
- Update essential concepts if core changes
- Update quick reference patterns
- Keep under ~300 lines

**Detailed Files**:

1. **CORE_CONCEPTS.md** - Update when:
   - New core component added
   - Existing component API changes
   - New pattern introduced

   **Sections**:
   - DragContext
   - Three-Level Rule Validation
   - Strategy Pattern
   - InventoryAcceptanceRequest
   - Preview Conversion Pipeline
   - InventoryTransferService
   - BaseSlot
   - Dynamic Slot Management
   - DataBinding System

2. **OPERATIONS.md** - Update when:
   - Operation flow changes
   - New operation type added

   **Sections**:
   - Manual Drag & Drop Flow
   - Auto-Transfer Flow
   - Swap Operation
   - Quick Click Recognition
   - Relocation System

3. **ADVANCED_FEATURES.md** - Update when:
   - New advanced feature added
   - Extension points change

   **Sections**:
   - Quick Click Auto-Transfer
   - Animation System
   - 3D World Integration
   - Tooltip System
   - Extension Points

4. **EXAMPLES.md** - Update when:
   - New demo added
   - Demo functionality changes

   **Sections**:
   - Demo1: Basic Inventory
   - Demo2: Trading System
   - Demo3: Loot System

---

### Step 3: Update dragdrop-architecture Skill

**SKILL.md (Quick Reference)**:
- Update component overview
- Update strategy hierarchy
- Update key architectural changes section
- Keep under ~300 lines

**Detailed Files**:

1. **COMPONENTS.md** - Update when:
   - New component class added
   - Component responsibility changes

   **Add full documentation**:
   ```markdown
   ## ComponentName (NEW!)

   **Responsibility**: What it does

   **Location**: `Scripts/Path/To/Component.cs`

   **Structure**:
   ```csharp
   // Code example
   ```

   **Benefits**: Why it exists
   ```

2. **STRATEGIES.md** - Update when:
   - New strategy added
   - Strategy behavior changes

   **Sections**:
   - Strategy Hierarchy
   - UniqueItemStrategy
   - StackableItemStrategy
   - SeparableStacksStrategy
   - DynamicSlotDecorator

3. **DATA_FLOW.md** - Update when:
   - Operation pipeline changes
   - New operation added

   **Update flow diagrams** with ASCII art

4. **PERFORMANCE.md** - Update when:
   - New hot path identified
   - Optimization implemented
   - Performance characteristics change

---

### Step 4: Update dragdrop-expert Skill

**SKILL.md (Quick Reference)**:
- Update "Key Architectural Changes" section
- Update decision tree if needed
- Update code review checklist
- Keep under ~310 lines

**Detailed Files**:

1. **ANTIPATTERNS.md** - Update when:
   - New anti-pattern discovered
   - Old pattern no longer applies

   **Add new anti-patterns**:
   ```markdown
   ## Anti-Pattern #N: Title

   ❌ **BAD**:
   ```csharp
   // Bad example
   ```

   ✅ **GOOD**:
   ```csharp
   // Good example
   ```
   ```

2. **BEST_PRACTICES.md** - Update when:
   - New extension point added
   - Optimization strategy discovered

   **Sections**:
   - Extension Points
   - Performance Optimizations
   - Asset Store Best Practices

3. **TESTING.md** - Update when:
   - New feature needs testing
   - Test scenarios change

   **Add test cases** for new features

---

### Step 5: Verify Consistency

Before reporting completion, follow
[Compilation And Test Verification](../VERIFICATION.md).

At minimum:

1. build runtime, editor test assembly, and examples sequentially;
2. run the narrowest affected Unity EditMode fixture;
3. run the full plugin EditMode assembly for architectural changes;
4. if the project is open in Unity, report the lock and do not claim tests passed;
5. distinguish compilation from executed tests in the final report.

**Cross-check all skills**:

```bash
# Check for inconsistencies
grep -n "ISlot.*interface" .claude/skills/*/SKILL.md .claude/skills/*/*.md
# Should find NONE (slot base is BaseSlot; ISlot is used only for filter/sorter contracts)

grep -n "InventoryTransferService" .claude/skills/*/SKILL.md .claude/skills/*/*.md
# Should find in ALL main skills

# Check dates are updated
grep "Last Updated" .claude/skills/*/SKILL.md

# Check for broken internal references
grep -o "\[.*\.md\]" .claude/skills/*/SKILL.md | while read link; do
  file=$(echo $link | sed 's/\[\(.*\)\.md\]/\1.md/')
  [ ! -f ".claude/skills/$file" ] && echo "BROKEN: $file"
done
```

**Consistency checklist**:
- [ ] Same component described identically across skills
- [ ] Code examples use current API
- [ ] File paths are correct
- [ ] Internal links work (SKILL.md → detailed files)
- [ ] "Last Updated" dates are current
- [ ] NEW! markers on recent additions
- [ ] No contradicting information

---

## Common Update Scenarios

### Scenario 1: New Component Added

**Example**: `InventoryTransferService` was added

**Steps**:
1. ✅ Update **dragdrop-system/SKILL.md**: Add to core architecture overview
2. ✅ Update **dragdrop-system/CORE_CONCEPTS.md**: Add full section with examples
3. ✅ Update **dragdrop-architecture/SKILL.md**: Add to component overview
4. ✅ Update **dragdrop-architecture/COMPONENTS.md**: Add detailed documentation
5. ✅ Update **dragdrop-architecture/DATA_FLOW.md**: Update transaction pipeline
6. ✅ Update **dragdrop-expert/SKILL.md**: Add to "Key Architectural Changes"
7. ✅ Update **dragdrop-expert/BEST_PRACTICES.md**: Add usage examples
8. ✅ Update all "Last Updated" dates

---

### Scenario 2: Component Refactored

**Example**: slot model moved from `ISlot`-based base class docs to `BaseSlot`

**Steps**:
1. ✅ Update **dragdrop-system/SKILL.md**: Update quick reference
2. ✅ Update **dragdrop-system/CORE_CONCEPTS.md**: Update BaseSlot section, add migration notes
3. ✅ Update **dragdrop-architecture/COMPONENTS.md**: Update type description with ⚠️ marker
4. ✅ Update **dragdrop-expert/SKILL.md**: Add to architectural changes
5. ✅ Update **dragdrop-expert/ANTIPATTERNS.md**: Add migration anti-patterns if needed
6. ✅ Search and replace ALL references in all files

**Search and verify**:
```bash
# Find all BaseSlot / ISlot references
grep -rn "ISlot" .claude/skills/

# Ensure slot base references point to BaseSlot
grep -rn "ISlot.*interface" .claude/skills/
# Should return NOTHING
```

---

### Scenario 3: New Demo Added

**Example**: Demo4 for networking added

**Steps**:
1. ✅ Update **dragdrop-system/SKILL.md**: Add to Examples section
2. ✅ Update **dragdrop-system/EXAMPLES.md**: Add full demo breakdown
3. ✅ Update **dragdrop-expert/TESTING.md**: Add integration tests for demo

---

### Scenario 4: Performance Optimization

**Example**: Dirty flags implemented

**Steps**:
1. ✅ Update **dragdrop-architecture/PERFORMANCE.md**: Remove "TODO", add implementation
2. ✅ Update **dragdrop-expert/BEST_PRACTICES.md**: Add optimization example
3. ✅ Update **dragdrop-expert/SKILL.md**: Update performance tips (remove TODO)

---

## Automation Scripts

### Check for Outdated Skills

Save as `.claude/skills/check-outdated.sh`:

```bash
#!/bin/bash

# Find code files modified after skills
SKILLS_DIR=".claude/skills"
NEWEST_SKILL=$(find $SKILLS_DIR -name "*.md" -type f -printf '%T@ %p\n' | sort -n | tail -1 | cut -d' ' -f2)
SKILL_TIME=$(stat -c %Y "$NEWEST_SKILL")

echo "🔍 Checking for code changes after last skill update..."
echo "Last skill updated: $NEWEST_SKILL"

# Find newer .cs files in critical directories
OUTDATED_FILES=$(find Scripts/Core Scripts/Inventories Scripts/Slots Scripts/Rules \
  -name "*.cs" -type f -newermt "@$SKILL_TIME" 2>/dev/null)

if [ -n "$OUTDATED_FILES" ]; then
  echo ""
  echo "⚠️  Code files modified after skills:"
  echo "$OUTDATED_FILES"
  echo ""
  echo "📝 Skills may need updating!"
  exit 1
else
  echo "✅ Skills are up to date"
  exit 0
fi
```

### Verify Internal Links

Save as `.claude/skills/verify-links.sh`:

```bash
#!/bin/bash

echo "🔗 Verifying internal links..."

cd ".claude/skills"

# Extract all markdown links
grep -roh "\[.*\](\.\/.*\.md)" --include="*.md" | while read link; do
  # Extract file path
  file=$(echo "$link" | sed 's/.*](\.\///' | sed 's/).*//')

  # Get directory of the file containing the link
  dir=$(dirname "$file")

  # Check if file exists
  if [ ! -f "$file" ]; then
    echo "❌ BROKEN LINK: $link"
  fi
done

echo "✅ Link verification complete"
```

---

## Best Practices for Maintenance

### When Updating

✅ **DO**:
- Update SKILL.md AND detailed files together
- Test code examples for compilation
- Add concrete examples, not abstract descriptions
- Update "Last Updated" dates
- Add "NEW!" or "⚠️ CHANGED!" markers
- Keep SKILL.md files under 350 lines
- Use internal links to detailed files
- Cross-reference between skills

❌ **DON'T**:
- Update one file and forget others
- Use placeholder code that doesn't compile
- Remove information without replacement
- Leave outdated examples
- Forget to update diagrams
- Break existing links

### Documentation Style

**In SKILL.md (Quick Reference)**:
- Brief overviews only
- Link to detailed files with 📖 **See**: syntax
- Essential code snippets only
- Keep it scannable

**In Detailed Files**:
- Complete explanations
- Full code examples
- Comprehensive scenarios
- Can be longer and thorough

---

## Verification Checklist

After updating skills:

### Content Accuracy
- [ ] All file paths are correct (`Scripts/...`)
- [ ] All class names match current code
- [ ] All method signatures match current API
- [ ] All enum values match current code
- [ ] Component diagrams reflect current structure
- [ ] Code examples compile

### Consistency
- [ ] Same component described identically across skills
- [ ] No contradicting statements between skills
- [ ] Code examples use consistent style
- [ ] Terminology is consistent
- [ ] Internal links work

### Completeness
- [ ] All new public classes documented
- [ ] All new public methods documented
- [ ] All new enums/structs documented
- [ ] All new patterns explained
- [ ] Migration paths provided for breaking changes

### Structure
- [ ] SKILL.md files are under 350 lines
- [ ] Detailed files are well-organized
- [ ] Internal links use correct relative paths
- [ ] Files follow naming conventions

### Metadata
- [ ] "Last Updated" dates current in SKILL.md files
- [ ] "NEW!" markers on recent additions
- [ ] YAML frontmatter correct
- [ ] Description fields accurate

---

## Quick Reference Commands

```bash
# Check if skills need update
.claude/skills/check-outdated.sh

# Verify internal links
.claude/skills/verify-links.sh

# Find all references to a component
grep -rn "InventoryTransferService" .claude/skills/

# Check for outdated terminology
grep -rn "ISlot.*interface" .claude/skills/
# Should return NOTHING

# Count lines in SKILL.md files
wc -l .claude/skills/*/SKILL.md
# Should all be under 350 lines

# Check dates
grep "Last Updated" .claude/skills/*/SKILL.md
```

---

## Summary

**Key Points**:

1. **New modular structure**: SKILL.md (quick) + detailed files (deep)
2. Monitor code changes in Scripts/Core, Scripts/Inventories, Scripts/Slots
3. Update SKILL.md AND detailed files together
4. Verify with automated scripts
5. Test code examples
6. Keep "Last Updated" dates current
7. Use markers for recent changes (NEW!, ⚠️ CHANGED!)
8. Run periodic audits every 2-3 months

**Remember**: Skills are living documentation. They must evolve with the codebase to remain helpful!

---

**This maintenance skill should be updated whenever the maintenance procedures themselves change!** 🔄
