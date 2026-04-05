# Showcase

This subsection is intended for **real projects using the asset**: games, prototypes, vertical slices, jam builds, and internal tools.

You can place here:

- screenshots
- GIFs showing gameplay or UI flow
- YouTube videos and devlogs
- links to Steam, itch.io, GitHub, ArtStation, or a project website

---

## What each case study should show

Each project entry works best if it follows the same minimal structure:

1. Project name
2. Short game or tool description
3. How the asset is used
4. 1-3 images or GIFs
5. Video or YouTube link
6. External links to the project

---

## Recommended file layout

It is convenient to keep media next to the docs, for example:

```text
.docs/content/assets/showcase/project-name/
  cover.jpg
  inventory.gif
  trading.gif
```

Then reference them directly in markdown:

```md
![Project cover](../assets/showcase/project-name/cover.jpg)
```

---

## Project cards

Below is a template you can copy for new case studies.

<div class="showcase-grid">
  <div class="showcase-card">
    <h3>Project Name</h3>
    <p><strong>Genre:</strong> RPG / Survival / Sandbox / Tool</p>
    <p><strong>Status:</strong> Prototype / In Development / Released</p>
    <p>A short 2-3 sentence description of the project. Explain what it is and where the inventory system is used.</p>

    <div class="showcase-media">
      <img src="/assets/showcase/placeholder-cover.svg" alt="Project screenshot placeholder">
    </div>

    <p><strong>How the asset is used:</strong></p>
    <ul>
      <li>player drag & drop inventory</li>
      <li>containers, chests, or trading</li>
      <li>equipment, split stacks, swap, context menu</li>
    </ul>

    <div class="showcase-links">
      <a href="https://youtube.com/" target="_blank" rel="noopener">YouTube</a>
      <a href="https://itch.io/" target="_blank" rel="noopener">itch.io</a>
      <a href="https://store.steampowered.com/" target="_blank" rel="noopener">Steam</a>
    </div>
  </div>
</div>

---

## GIF example

For short UI flows, a GIF is often easier to scan than a full video:

```md
![Trading flow](../assets/showcase/project-name/trading-flow.gif)
```

---

## YouTube embed example

If you want a playable video directly on the page, use an iframe:

```html
<div class="showcase-video">
  <iframe
    src="https://www.youtube.com/embed/VIDEO_ID"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
  </iframe>
</div>
```

Example block:

<div class="showcase-video">
  <iframe
    src="https://www.youtube.com/embed/VIDEO_ID"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
  </iframe>
</div>

---

## What to avoid

- Do not place too many heavy GIFs on one page.
- Do not mix too many topics in one case study. Keep the focus on where the inventory system is used.
- Do not leave raw URLs without labels. Prefer readable links such as `Steam`, `YouTube`, `Devlog`, or `Repository`.

---

## Recommended long-term structure

If the number of projects grows, it is better to move from one long page to this structure:

```text
.docs/content/examples/showcase.md
.docs/content/examples/showcase/
  project-a.md
  project-b.md
  project-c.md
```

Then keep the current page as a gallery/index that links to individual project pages.
