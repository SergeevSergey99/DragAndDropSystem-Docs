/** Item icon sets for different themes. Simple emoji placeholders so no SVG slop. */

const THEMES = {
  rpg: {
    sword: '⚔️', shield: '🛡️', bow: '🏹',
    potion: '🧪', potion2: '🍷', potion3: '🫗',
    gold: '🪙', gem: '💎', ring: '💍',
    scroll: '📜', book: '📕', key: '🗝️',
    apple: '🍎', meat: '🍖', bread: '🥖',
    helmet: '⛑️', boots: '🥾', armor: '🦺',
  },
  scifi: {
    chip: '💾', battery: '🔋', card: '💳',
    bolt: '⚡', target: '🎯', radar: '📡',
    tool: '🔧', gear: '⚙️', magnet: '🧲',
    module: '📦', cube: '🧊', atom: '⚛️',
    laser: '🔦', gun: '🔫', shield: '🛡️',
  },
  survival: {
    wood: '🪵', stone: '🪨', ore: '⛏️',
    apple: '🍎', meat: '🍖', fish: '🐟', bread: '🍞',
    axe: '🪓', pickaxe: '⛏️', bow: '🏹',
    torch: '🔥', water: '💧', seed: '🌱',
    grass: '🌿', bucket: '🪣', map: '🗺️',
  }
};

// Pick items for a slide — intentionally mix themes on the "any data" slide, etc.
function pickItems(theme = 'rpg', n = 10) {
  const set = THEMES[theme] || THEMES.rpg;
  const keys = Object.keys(set);
  const out = [];
  for (let i = 0; i < n; i++) out.push(set[keys[i % keys.length]]);
  return out;
}

window.THEMES = THEMES;
window.pickItems = pickItems;
