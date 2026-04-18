/** Primitives: Slot, Grid, Tag, Frame, Arrow. Shared across slides. */

const Slot = ({ children, size = 96, state = 'empty', highlight = false, style = {}, dragFrom = false, dragOver = false, selected = false }) => {
  const bg = state === 'empty' ? 'var(--slot)' : 'var(--slot-hi)';
  const borderColor = dragOver ? 'var(--accent)' : selected ? 'var(--accent-2)' : dragFrom ? 'var(--accent)' : 'var(--border)';
  return (
    <div style={{
      width: size, height: size, background: bg,
      border: `1.5px solid ${borderColor}`,
      boxShadow: dragOver
        ? `0 0 0 3px rgba(245,165,36,0.25), inset 0 1px 0 rgba(255,255,255,0.04)`
        : selected
        ? `0 0 0 2px rgba(62,198,177,0.35), inset 0 1px 0 rgba(255,255,255,0.04)`
        : `inset 0 1px 0 rgba(255,255,255,0.04), 0 1px 2px rgba(0,0,0,0.4)`,
      borderRadius: 8,
      position: 'relative',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: size * 0.5,
      ...style,
    }}>
      {children}
      {dragFrom && (
        <div style={{position:'absolute',inset:0,borderRadius:8,background:'rgba(245,165,36,0.08)',pointerEvents:'none'}}/>
      )}
    </div>
  );
};

const SlotWithCount = ({ icon, count, size = 96, ...rest }) => (
  <Slot size={size} state={icon ? 'filled' : 'empty'} {...rest}>
    {icon && <span style={{lineHeight:1}}>{icon}</span>}
    {count > 1 && (
      <span className="mono" style={{
        position:'absolute', right: 6, bottom: 4,
        fontSize: size*0.16, color:'var(--text)',
        textShadow:'0 1px 2px rgba(0,0,0,0.9)',
        fontWeight:600,
      }}>{count}</span>
    )}
  </Slot>
);

const Grid = ({ cols, rows, cells = [], size = 96, gap = 8, dragFrom, dragOver, selected = [] }) => {
  const total = cols * rows;
  const arr = Array.from({length: total}, (_, i) => cells[i] || null);
  return (
    <div style={{
      display:'grid',
      gridTemplateColumns: `repeat(${cols}, ${size}px)`,
      gridTemplateRows: `repeat(${rows}, ${size}px)`,
      gap,
    }}>
      {arr.map((c, i) => (
        <SlotWithCount key={i} size={size}
          icon={c?.icon} count={c?.count || 1}
          dragFrom={dragFrom === i}
          dragOver={dragOver === i}
          selected={selected.includes(i)}
        />
      ))}
    </div>
  );
};

const Panel = ({ title, subtitle, children, style = {}, accent = 'var(--accent)', compact = false }) => (
  <div style={{
    background: 'var(--panel)',
    border: '1px solid var(--border)',
    borderRadius: 14,
    padding: compact ? '18px 22px' : '26px 28px',
    boxShadow: '0 30px 60px -20px rgba(0,0,0,0.6), inset 0 1px 0 rgba(255,255,255,0.03)',
    ...style,
  }}>
    {title && (
      <div style={{display:'flex', alignItems:'center', gap:12, marginBottom: compact ? 12 : 18}}>
        <div style={{width:8, height:8, borderRadius:2, background: accent}}/>
        <div className="mono" style={{fontSize:13, letterSpacing:'0.14em', color:'var(--muted)', textTransform:'uppercase'}}>{title}</div>
        {subtitle && <div style={{fontSize:13, color:'var(--dim)'}}>{subtitle}</div>}
      </div>
    )}
    {children}
  </div>
);

const Tag = ({ children, color = 'var(--accent)', filled = false, style = {} }) => (
  <span className="mono" style={{
    display:'inline-flex', alignItems:'center', gap:6,
    padding:'4px 10px',
    fontSize:12,
    letterSpacing:'0.06em',
    textTransform:'uppercase',
    borderRadius:999,
    border:`1px solid ${color}`,
    color: filled ? 'var(--ink)' : color,
    background: filled ? color : 'transparent',
    fontWeight:600,
    ...style,
  }}>{children}</span>
);

const SlideFrame = ({ children, className = 'bg-dots', style = {} }) => (
  <div className={className} style={{
    width:'100%', height:'100%',
    padding: '90px 120px 90px 120px',
    display:'flex', flexDirection:'column',
    position:'relative',
    ...style,
  }}>
    {children}
  </div>
);

const HeaderBar = ({ eyebrow, title, sub, right }) => (
  <div style={{display:'flex', alignItems:'flex-end', justifyContent:'space-between', marginBottom: 48}}>
    <div>
      {eyebrow && <div className="mono" style={{fontSize:15, letterSpacing:'0.22em', color:'var(--accent)', textTransform:'uppercase', marginBottom:18, fontWeight:600}}>{eyebrow}</div>}
      <h1 style={{fontSize:76, fontWeight:700, margin:0, letterSpacing:'-0.02em', lineHeight:1.02, textWrap:'balance'}}>{title}</h1>
      {sub && <p style={{fontSize:26, color:'var(--muted)', margin:'22px 0 0 0', maxWidth:820, lineHeight:1.35}}>{sub}</p>}
    </div>
    {right}
  </div>
);

const Arrow = ({ dir='right', color='var(--muted)', size=20, style={} }) => {
  const rot = dir==='right'?0:dir==='down'?90:dir==='left'?180:270;
  return (
    <svg width={size} height={size} viewBox="0 0 20 20" style={{transform:`rotate(${rot}deg)`, ...style}}>
      <path d="M3 10h12M10 5l5 5-5 5" stroke={color} strokeWidth="1.8" fill="none" strokeLinecap="round" strokeLinejoin="round"/>
    </svg>
  );
};

const CornerMark = ({ children }) => (
  <div style={{
    position:'absolute', left:60, bottom:50,
    display:'flex', alignItems:'center', gap:14,
    fontSize:14, color:'var(--dim)',
  }}>
    <div style={{width:24, height:1, background:'var(--border-hi)'}}/>
    <span className="mono" style={{letterSpacing:'0.12em', textTransform:'uppercase'}}>{children}</span>
  </div>
);

const PageTag = ({ n, total=8 }) => (
  <div style={{
    position:'absolute', right:60, top:50,
    display:'flex', alignItems:'center', gap:10,
  }}>
    <span className="mono" style={{fontSize:13, color:'var(--dim)', letterSpacing:'0.1em'}}>UNIVERSAL DRAG & DROP</span>
    <span style={{width:1, height:14, background:'var(--border-hi)'}}/>
    <span className="mono" style={{fontSize:13, color:'var(--muted)', letterSpacing:'0.1em'}}>{String(n).padStart(2,'0')} / {String(total).padStart(2,'0')}</span>
  </div>
);

Object.assign(window, { Slot, SlotWithCount, Grid, Panel, Tag, SlideFrame, HeaderBar, Arrow, CornerMark, PageTag });
