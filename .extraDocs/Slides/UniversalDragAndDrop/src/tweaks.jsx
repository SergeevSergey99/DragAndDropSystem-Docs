/** Tweaks panel — palette + item theme */

const PALETTES = {
  amber:   { accent:'#F5A524', accent2:'#3EC6B1', accent3:'#E05CD9', label:'Amber / Teal'   },
  indigo:  { accent:'#7C7BFF', accent2:'#3EC6B1', accent3:'#F5A524', label:'Indigo'         },
  crimson: { accent:'#E5484D', accent2:'#F5D144', accent3:'#3EC6B1', label:'Crimson'        },
  mint:    { accent:'#6EE787', accent2:'#3EC6B1', accent3:'#F5A524', label:'Mint'           },
  mono:    { accent:'#E6E7EA', accent2:'#8A8E96', accent3:'#5A5E66', label:'Mono'           },
};

function applyPalette(key) {
  const p = PALETTES[key] || PALETTES.amber;
  const r = document.documentElement.style;
  r.setProperty('--accent', p.accent);
  r.setProperty('--accent-2', p.accent2);
  r.setProperty('--accent-3', p.accent3);
}

function TweaksPanel({ onClose, onChange, state }) {
  return (
    <div style={{
      position:'fixed', right:20, bottom:80, zIndex:9999,
      background:'rgba(18,19,22,0.96)',
      border:'1px solid var(--border)',
      borderRadius:14, padding:'18px 20px',
      width:300, color:'var(--text)',
      fontFamily:'Inter, system-ui, sans-serif',
      boxShadow:'0 30px 60px rgba(0,0,0,0.6)',
      backdropFilter:'blur(8px)',
    }}>
      <div style={{display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom:14}}>
        <span className="mono" style={{fontSize:12, letterSpacing:'0.15em', color:'var(--muted)', textTransform:'uppercase'}}>Tweaks</span>
        <button onClick={onClose} style={{background:'transparent', border:'none', color:'var(--muted)', fontSize:18, cursor:'pointer'}}>×</button>
      </div>

      <div style={{fontSize:12, color:'var(--muted)', marginBottom:8, textTransform:'uppercase', letterSpacing:'0.1em'}}>Palette</div>
      <div style={{display:'grid', gridTemplateColumns:'1fr 1fr', gap:6, marginBottom:18}}>
        {Object.entries(PALETTES).map(([key,p])=>(
          <button key={key} onClick={()=>onChange('palette', key)}
            style={{
              display:'flex', alignItems:'center', gap:8,
              padding:'8px 10px',
              background: state.palette===key ? 'var(--panel-2)' : 'var(--panel)',
              border:`1px solid ${state.palette===key ? p.accent : 'var(--border)'}`,
              borderRadius:8, color:'var(--text)', cursor:'pointer',
              fontSize:12, fontFamily:'inherit',
            }}>
            <div style={{display:'flex', gap:3}}>
              <div style={{width:10, height:10, borderRadius:2, background:p.accent}}/>
              <div style={{width:10, height:10, borderRadius:2, background:p.accent2}}/>
              <div style={{width:10, height:10, borderRadius:2, background:p.accent3}}/>
            </div>
            <span>{p.label}</span>
          </button>
        ))}
      </div>

      <div style={{fontSize:12, color:'var(--muted)', marginBottom:8, textTransform:'uppercase', letterSpacing:'0.1em'}}>Item theme</div>
      <div style={{display:'grid', gridTemplateColumns:'1fr 1fr', gap:6}}>
        {[
          {k:'rpg', l:'RPG'},
          {k:'survival', l:'Survival'},
          {k:'scifi', l:'Sci‑Fi'},
          {k:'mixed', l:'Mixed'},
        ].map(o=>(
          <button key={o.k} onClick={()=>onChange('itemTheme', o.k)}
            style={{
              padding:'8px 10px',
              background: state.itemTheme===o.k ? 'var(--panel-2)' : 'var(--panel)',
              border:`1px solid ${state.itemTheme===o.k ? 'var(--accent)' : 'var(--border)'}`,
              borderRadius:8, color:'var(--text)', cursor:'pointer',
              fontSize:12, fontFamily:'inherit',
            }}>{o.l}</button>
        ))}
      </div>
    </div>
  );
}

window.TweaksPanel = TweaksPanel;
window.PALETTES = PALETTES;
window.applyPalette = applyPalette;
