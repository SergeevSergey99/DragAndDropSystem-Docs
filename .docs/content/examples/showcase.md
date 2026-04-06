# Showcase

Этот подраздел предназначен для **реальных проектов, использующих ассет**: игр, прототипов, vertical slice, джем-версий и внутренних tools.

Сюда можно добавлять:

- скриншоты
- gif с геймплеем или UI
- YouTube-видео и devlog
- ссылки на Steam, itch.io, GitHub, ArtStation или сайт проекта

---

## Что стоит показывать в каждом кейсе

Для каждого проекта лучше держать один и тот же минимальный набор:

1. Название проекта
2. Короткое описание игры или инструмента
3. Как именно используется ассет
4. 1-3 изображения или gif
5. Видео или ссылка на YouTube
6. Ссылки на страницу проекта

---

## Рекомендуемая структура файлов

Медиа удобно складывать рядом с документацией, например:

```text
.docs/content/assets/showcase/project-name/
  cover.jpg
  inventory.gif
  trading.gif
```

Тогда в markdown можно ссылаться на них напрямую:

```md
![Project cover](../assets/showcase/project-name/cover.jpg)
```

---

## Карточки проектов

Ниже шаблон, который можно копировать для новых кейсов.

<div class="showcase-grid">
  <div class="showcase-card">
    <h3>Violent Horror Stories 2 / The Exit Is Inside</h3>
    <p><strong>Жанр:</strong> Horror</p>
    <p><strong>Статус:</strong> Released</p>
    <p>Короткое описание проекта в 2-3 предложениях. Что это за игра, какой у неё сеттинг и в каком контексте используется inventory.</p>

    <div class="showcase-media">
      <img src="../../assets/showcase/placeholder-cover.svg" alt="Project screenshot placeholder">
    </div>

    <div class="showcase-video">
      <iframe
        src="https://www.youtube.com/embed/YWx4Bcbx7kI"
        title="Project showcase video"
        allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
        allowfullscreen>
      </iframe>
    </div>

    <p><strong>Как используется ассет:</strong></p>
    <ul>
      <li>drag & drop инвентарь игрока</li>
      <li>контейнеры, сундуки или торговля</li>
      <li>экипировка, split stacks, swap, context menu</li>
    </ul>

    <div class="showcase-links">
      <a href="https://store.steampowered.com/app/3636960/Violent_Horror_Stories_2/" target="_blank" rel="noopener">Steam</a>
    </div>
  </div>
</div>

---

## Пример блока с gif

Если нужно показать короткий UI-flow, gif обычно читается быстрее видео:

```md
![Trading flow](../assets/showcase/project-name/trading-flow.gif)
```

---

## Пример встраивания YouTube

Если нужен ролик прямо на странице, можно использовать iframe:

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

Пример блока:

<div class="showcase-video">
  <iframe
    src="https://www.youtube.com/embed/VIDEO_ID"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
  </iframe>
</div>

---

## Что лучше не делать

- Не вставлять слишком много тяжёлых gif подряд на одной странице.
- Не смешивать в одном кейсе 10 разных тем. Лучше кратко показать, где именно используется inventory.
- Не использовать raw URL без подписи. Лучше давать понятные ссылки: `Steam`, `YouTube`, `Devlog`, `Repository`.

---

## Рекомендация по структуре кейсов

Если проектов станет много, лучше перейти с одной длинной страницы на структуру:

```text
.docs/content/examples/showcase.md
.docs/content/examples/showcase/
  project-a.md
  project-b.md
  project-c.md
```

А текущую страницу оставить как индекс-галерею со ссылками на отдельные проекты.
