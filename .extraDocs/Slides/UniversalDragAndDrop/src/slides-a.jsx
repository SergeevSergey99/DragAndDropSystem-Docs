/** Slides 1-4 */

// ============ SLIDE 1: HERO COVER ============
function Slide1() {
  const theme = window.TWEAKS.itemTheme === 'mixed' ? 'rpg' : window.TWEAKS.itemTheme;
  const T = THEMES[theme] || THEMES.rpg;
  const keys = Object.keys(T);

  // Two stylized inventories, with a drag animation between them.
  const leftGrid = [
    {icon: T[keys[0]]}, {icon: T[keys[1]]}, {icon: T[keys[2]], count:5}, null,
    {icon: T[keys[3]]}, null, {icon: T[keys[4]], count:3}, null,
    null, {icon: T[keys[5]]}, {icon: T[keys[6]]}, null,
  ];
  const rightGrid = [
    {icon: T[keys[7]] || T[keys[0]]}, null, {icon: T[keys[8]] || T[keys[1]], count:12}, null,
    null, {icon: T[keys[9]] || T[keys[2]]}, null, {icon: T[keys[10]] || T[keys[3]]},
    {icon: T[keys[11]] || T[keys[4]], count:2}, null, null, null,
  ];

  return (
    <SlideFrame className="bg-grid" style={{padding:0, overflow:'hidden'}}>
      
      {/* Left big text panel */}
      <div style={{position:'absolute', left:280, top:0, bottom:0, width:900, display:'flex', flexDirection:'column', justifyContent:'center', zIndex:3}}>
        <h1 style={{fontSize:130, fontWeight:800, margin:0, letterSpacing:'-0.035em', lineHeight:0.95, textWrap:'balance'}}>
          Universal<br/>Drag<span style={{color:'var(--accent)'}}>&</span>Drop
        </h1>
        <div style={{marginTop:44, display:'flex', gap:10, flexWrap:'wrap'}}>
          <Tag color="var(--accent)">Any data</Tag>
          <Tag color="var(--accent-2)">Stacks & swaps</Tag>
          <Tag color="var(--accent-3)">Rules • Converters</Tag>
          <Tag color="var(--muted)">6 demos included</Tag>
        </div>
      </div>

      {/* Right: two inventories with drag-line */}
      <div style={{position:'absolute', right:70, top:120, bottom:120, width:1020, display:'flex', alignItems:'center', justifyContent:'center'}}>
        <div style={{position:'relative', transform:'rotate(-6deg)', transformOrigin:'center'}}>
          <Panel title="Backpack" style={{marginRight:240, marginBottom:200}}>
            <Grid cols={4} rows={3} cells={leftGrid} size={92} dragFrom={2}/>
          </Panel>
          <div style={{position:'absolute', right:110, bottom:-20, transform:'rotate(10deg)'}}>
            <Panel title="Chest" accent="var(--accent-2)">
              <Grid cols={4} rows={3} cells={rightGrid} size={92} dragOver={3}/>
            </Panel>
          </div>

          {/* Ghost drag item mid-flight */}
          <div style={{
            position:'absolute', right:230, top:190,
            width:92, height:92, borderRadius:10,
            background:'var(--slot-hi)',
            border:'1.5px solid var(--accent)',
            boxShadow:'0 20px 40px rgba(0,0,0,0.6), 0 0 0 3px rgba(245,165,36,0.25)',
            display:'flex', alignItems:'center', justifyContent:'center',
            fontSize:48, transform:'rotate(8deg)',
          }}>
            {T[keys[2]]}
            <div style={{position:'absolute', right:6, bottom:4}} className="mono">
              <span style={{fontSize:14, color:'var(--text)', fontWeight:600, textShadow:'0 1px 2px rgba(0,0,0,0.9)'}}>5</span>
            </div>
          </div>

          {/* Dotted trail */}
          {/*Как это работает:

              Кривая задается атрибутом d у path:
              M60 40 C 180 40, 220 260, 380 300
              Это кубическая Безье-кривая:
              M60 40 — стартовая точка.
              C ... — две контрольные точки и конечная точка, которые формируют изгиб.
              Пунктир задается strokeDasharray="2 8":
              2 — длина штриха.
              8 — длина промежутка.
              Поэтому линия выглядит как редкие точки/короткие штрихи.
              Круглые концы у штрихов задает strokeLinecap="round", из-за чего пунктир выглядит мягче и более как дорожка следа.

              Цвет и прозрачность:

              stroke="var(--accent)"
              opacity="0.55
           */}
          <svg style={{position:'absolute', right:0, top:100, pointerEvents:'none'}} width="460" height="360" viewBox="0 0 460 360">
            <path d="M50 20 C 180 40, 220 260, 285 260"
              fill="none" stroke="var(--accent)" strokeWidth="2"
              strokeDasharray="2 8" strokeLinecap="round" opacity="0.55"/>
          </svg>
        </div>
      </div>

    </SlideFrame>
  );
}

// ============ SLIDE 2: WHAT IT IS ============
function Slide2() {
  const bullets = [
    {icon:'▸', title:'Any data source', text:'ScriptableObject, runtime models, lists, dictionaries, or fixed fields.'},
    {icon:'▸', title:'UI decoupled from model', text:'Scales gradually — from a backpack to trading, nested containers, server validation.'},
    {icon:'▸', title:'Full transfer pipeline', text:'Drag, drop, stack, swap, split, auto‑transfer, multi‑select — all handled.'},
    {icon:'▸', title:'Rules + business hooks', text:'Mechanical rules, domain checks before commit, and optional async validation.'},
  ];

  return (
    <SlideFrame className="bg-dots">
      <PageTag n={2}/>
      <HeaderBar
        eyebrow="01 — Overview"
        title={<>A system, not a slot kit.</>}
        sub="Most inventory assets force your data into their types. This one visualizes almost any data you already have, keeping your model intact."
      />

      <div style={{display:'grid', gridTemplateColumns:'1.05fr 1fr', gap:60, alignItems:'start', marginTop:10}}>
        {/* Bullets */}
        <div style={{display:'flex', flexDirection:'column', gap:28}}>
          {bullets.map((b,i)=>(
            <div key={i} style={{display:'flex', gap:20, alignItems:'flex-start'}}>
              <div style={{
                width:40, height:40, borderRadius:10,
                background:'var(--panel)', border:'1px solid var(--border)',
                display:'flex', alignItems:'center', justifyContent:'center',
                color:'var(--accent)', fontSize:20, fontWeight:700, flexShrink:0, marginTop:2,
              }}>
                {String(i+1).padStart(2,'0')}
              </div>
              <div>
                <div style={{fontSize:26, fontWeight:600, marginBottom:6, letterSpacing:'-0.01em'}}>{b.title}</div>
                <div style={{fontSize:20, color:'var(--muted)', lineHeight:1.45, maxWidth:560}}>{b.text}</div>
              </div>
            </div>
          ))}
        </div>

        {/* Right: stacked data-type cards */}
        <div style={{display:'flex', flexDirection:'column', gap:14, marginTop:10}}>
          {[
            {tag:'ScriptableObject', sub:'class ItemSO : ScriptableObject', col:'var(--accent)'},
            {tag:'Runtime model',   sub:'class WeaponInstance { … }',        col:'var(--accent-2)'},
            {tag:'List<T>',         sub:'List<ItemModel> _items',            col:'var(--accent-3)'},
            {tag:'Dictionary<K,V>', sub:'Dictionary<Slot, ItemModel>',       col:'var(--green)'},
            {tag:'Fixed fields',    sub:'public WeaponSO Weapon;',           col:'var(--muted)'},
          ].map((d,i)=>(
            <div key={i} style={{
              background:'var(--panel)', border:'1px solid var(--border)',
              borderRadius:12, padding:'18px 22px',
              display:'flex', alignItems:'center', gap:18,
              boxShadow:'0 6px 14px rgba(0,0,0,0.25)',
            }}>
              <div style={{width:6, height:44, borderRadius:3, background:d.col, flexShrink:0}}/>
              <div style={{flex:1}}>
                <div style={{fontSize:19, fontWeight:600}}>{d.tag}</div>
                <div className="mono" style={{fontSize:14, color:'var(--muted)', marginTop:3}}>{d.sub}</div>
              </div>
              <div style={{
                fontSize:12, color:d.col,
                border:`1px solid ${d.col}`, padding:'4px 10px', borderRadius:999,
              }} className="mono">supported</div>
            </div>
          ))}
          <div style={{marginTop:8, fontSize:15, color:'var(--dim)', lineHeight:1.5}}>
            The visual inventory is separated from your game model —
            write one small adapter and the system takes care of drag, drop, stacking, and event flow.
          </div>
        </div>
      </div>

      <CornerMark>What it is</CornerMark>
    </SlideFrame>
  );
}

// ============ SLIDE 3: ARCHITECTURE DIAGRAM ============
function Slide3() {
  // Node factory
  const Node = ({ title, sub, color='var(--accent)', custom=false, showYouWrite=true, style={} }) => (
    <div style={{
      background:'var(--panel)',
      border:`1.5px solid ${custom ? color : 'var(--border)'}`,
      borderRadius:14,
      padding:'22px 28px',
      minWidth:300,
      boxShadow:'0 12px 24px rgba(0,0,0,0.35)',
      position:'relative',
      ...style,
    }}>
      {custom && showYouWrite && (
        <div style={{position:'absolute', top:-10, right:18}}>
          <Tag color={color} filled>You write</Tag>
        </div>
      )}
      <div style={{display:'flex', alignItems:'center', gap:10, marginBottom:6}}>
        <div style={{width:8, height:8, borderRadius:2, background:color}}/>
        <div className="mono" style={{fontSize:13, letterSpacing:'0.1em', color: custom ? color : 'var(--muted)', textTransform:'uppercase', fontWeight:600}}>
          {custom ? 'Integration' : 'Built‑in'}
        </div>
      </div>
      <div style={{fontSize:24, fontWeight:600, letterSpacing:'-0.01em'}}>{title}</div>
      <div className="mono" style={{fontSize:14, color:'var(--muted)', marginTop:6, lineHeight:1.45}}>{sub}</div>
    </div>
  );

  return (
    <SlideFrame className="bg-dots">
      <PageTag n={3}/>
      <HeaderBar
        eyebrow="02 — Architecture"
        title="One diagram. Keep it in your head."
        sub="Most integrations come down to one binding and a tiny item adapter. The rest of the runtime stays built in."
      />

      <div style={{flex:1, position:'relative', display:'flex', alignItems:'center', justifyContent:'center'}}>

        {/* Nodes positioned absolutely to match svg */}
        <div style={{position:'absolute', left:0, top:80}}>
          <Node title="Universal Inventory" sub={'Manages UI state, transfers,\nitem distribution across slots.'} color="var(--accent)"/>
        </div>
        <div style={{position:'absolute', left:0, top:400}}>
          <Node title="Universal Slot" sub={'Holds an adapter + item count.\nFires pipeline events.'} color="var(--accent)"/>
        </div>

        <div style={{position:'absolute', left:670, top:360}}>
          <Node title="IItemAdapter" sub={'Tiny class. Stores a reference\nto your item data.'} color="var(--accent-3)" custom showYouWrite={false}/>
        </div>
        <div style={{position:'absolute', left:650, top:380}}>
          <Node title="IItemAdapter" sub={'Tiny class. Stores a reference\nto your item data.'} color="var(--accent-3)" custom showYouWrite={false}/>
        </div>
        <div style={{position:'absolute', left:630, top:400}}>
          <Node title="IItemAdapter" sub={'Tiny class. Stores a reference\nto your item data.'} color="var(--accent-3)" custom/>
        </div>

        <div style={{position:'absolute', left:680, top:80}}>
          <Node title="DataBinding" sub={'Syncs UI events back into\nyour game model.'} color="var(--accent-3)" custom/>
        </div>
        <div style={{position:'absolute', right:0, top:80}}>
          <Node title="Your data models" sub={'SO, runtime classes, lists,\ndictionaries — untouched.'} color="var(--accent-2)" custom/>
        </div>

        <div style={{position:'absolute', right:80, top:360}}>
          <Node title="Your data item" sub={'SO, runtime classes — untouched.'} color="var(--accent-2)" custom showYouWrite={false}/>
        </div>
        <div style={{position:'absolute', right:100, top:380}}>
          <Node title="Your data item" sub={'SO, runtime classes — untouched.'} color="var(--accent-2)" custom showYouWrite={false}/>
        </div>
        <div style={{position:'absolute', right:120, top:400}}>
          <Node title="Your data item" sub={'SO, runtime classes — untouched.'} color="var(--accent-2)" custom/>
        </div>

        {/* SVG lines underneath nodes */}
        <svg style={{position:'absolute', inset:0, width:'100%', height:'100%', pointerEvents:'none'}} viewBox="0 0 1680 720" preserveAspectRatio="none">
          <defs>
            <marker id="arr" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
              <path d="M0 0 L10 5 L0 10 z" fill="var(--border-hi)"/>
            </marker>
          </defs>
          {/* Inventory -> Slot */}
          <line x1="230" y1="250" x2="230" y2="420" stroke="var(--border-hi)" strokeWidth="2" markerEnd="url(#arr)"/>

          {/* Slot -> Adapter */}
          <line x1="510" y1="520" x2="620" y2="520" stroke="var(--border-hi)" strokeWidth="2" markerEnd="url(#arr)"/>
          {/* Slot -> Adapter */}
          <line x1="510" y1="520" x2="620" y2="500" stroke="var(--border-hi)" strokeWidth="2" markerEnd="url(#arr)"/>
          {/* Slot -> Adapter */}
          <line x1="510" y1="520" x2="620" y2="480" stroke="var(--border-hi)" strokeWidth="2" markerEnd="url(#arr)"/>

          {/* Adapter -> Item */}
          <line x1="1140" y1="480" x2="1220" y2="480" stroke="var(--border-hi)" strokeWidth="2" markerEnd="url(#arr)"/>
          {/* Adapter -> Item */}
          <line x1="1120" y1="500" x2="1220" y2="500" stroke="var(--border-hi)" strokeWidth="2" markerEnd="url(#arr)"/>
          {/* Adapter -> Item */}
          <line x1="1100" y1="520" x2="1220" y2="520" stroke="var(--border-hi)" strokeWidth="2" markerEnd="url(#arr)"/>

          {/* Inventory <-> Binding */}
          <line x1="570" y1="160" x2="670" y2="160" stroke="var(--accent)" strokeWidth="2.5" markerEnd="url(#arr)" markerStart="url(#arr)"/>
          {/* Binding <-> Data */}
          <line x1="1100" y1="160" x2="1170" y2="160" stroke="var(--accent)" strokeWidth="2.5" markerEnd="url(#arr)" markerStart="url(#arr)"/>
        </svg>
        {/* Legend */}
        <div style={{position:'absolute', right:40, bottom:30, display:'flex', gap:22, alignItems:'center'}}>
          <div style={{display:'flex', alignItems:'center', gap:8}}>
            <div style={{width:20, height:3, background:'var(--border-hi)'}}/>
            <span style={{fontSize:14, color:'var(--muted)'}}>internal flow</span>
          </div>
          <div style={{display:'flex', alignItems:'center', gap:8}}>
            <div style={{width:20, height:3, background:'var(--accent)'}}/>
            <span style={{fontSize:14, color:'var(--muted)'}}>bi‑directional sync</span>
          </div>
        </div>
      </div>

      <CornerMark>Architecture</CornerMark>
    </SlideFrame>
  );
}

// ============ SLIDE 4: ANY DATA ============
function Slide4() {
  return (
    <SlideFrame className="bg-dots">
      <PageTag n={4}/>
      <HeaderBar
        eyebrow="03 — Your data, your way"
        title={<>Three binding templates cover most inventory shapes.</>}
        sub={<>Pick the template that matches your data shape. Usually you only override 4 methods.</>}
      />

      <div style={{display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap:50, marginTop:-40}}>
        {[
          {
            name:'ListInventoryDataBinding',
            use:'Backpack, chest, loot pile',
            shape:'List<T>',
            col:'var(--accent)',
            demo: ['🎒','🍎','🧪','⚔️','🗝️','🪙','💎','📜',null , null, null, null],
          },
          {
            name:'SlotIndexedInventoryDataBinding',
            use:'Hotbar, crafting grid',
            shape:'T[] with fixed indices',
            col:'var(--accent-2)',
            demo: [null,'🪓',null,'⛏️','🪵',null,'🪨',null,null,'🔥','🌱',null],
          },
          {
            name:'MappedSlotInventoryDataBinding',
            use:'Equipment, quick‑bar',
            shape:'Dictionary<Key,T>',
            col:'var(--accent-3)',
            demo: ['HIDDEN','⛑️','HIDDEN',null,'⚔️','🦺','🛡️','HIDDEN','HIDDEN',null,'HIDDEN','💍'],
          },
        ].map((t,i)=>(
          <div key={i} style={{
            background:'var(--panel)',
            border:'1px solid var(--border)',
            borderRadius:16,
            padding:'28px 28px 24px',
            display:'flex', flexDirection:'column',
            boxShadow:'0 20px 40px -20px rgba(0,0,0,0.5)',
          }}>
            <div style={{display:'flex', alignItems:'center', gap:10, marginBottom:14}}>
              <div style={{width:10, height:10, borderRadius:3, background:t.col}}/>
              <span className="mono" style={{fontSize:12, letterSpacing:'0.12em', color:'var(--muted)', textTransform:'uppercase'}}>Template {String(i+1).padStart(2,'0')}</span>
            </div>
            <div className="mono" style={{fontSize:19, fontWeight:600, letterSpacing:'-0.01em', color:'var(--text)', marginBottom:12, lineHeight:1.2, wordBreak:'break-word'}}>
              {t.name}
            </div>
            <div style={{fontSize:16, color:'var(--muted)', marginBottom:4}}>Use for</div>
            <div style={{fontSize:22, color:'var(--text)', fontWeight:500, marginBottom:18}}>{t.use}</div>
            <div style={{fontSize:16, color:'var(--muted)', marginBottom:4}}>Data shape</div>
            <div className="mono" style={{fontSize:17, color:t.col, marginBottom:22}}>{t.shape}</div>

            {/* Mini demo grid */}
            <div style={{marginTop:'auto', padding:16, background:'var(--ink)', borderRadius:10, border:'1px solid var(--border)'}}>
              <div style={{display:'grid', gridTemplateColumns:'repeat(4, 1fr)', gap:6}}>
                {t.demo.map((c,j)=>{
                  const hidden = c === 'HIDDEN';
                  return (
                    <div key={j} style={{
                      aspectRatio:'1/1',
                      background: hidden ? 'transparent' : 'var(--slot)',
                      border: hidden ? '1px dashed rgba(255,255,255,0.08)' : '1px solid var(--border)',
                      borderRadius:6,
                      display:'flex', alignItems:'center', justifyContent:'center',
                      fontSize:26,
                      opacity: hidden ? 0.25 : 1,
                    }}>{hidden ? '' : (c||'')}</div>
                  );
                })}
              </div>
            </div>
          </div>
        ))}
      </div>

      <div style={{marginLeft:900, marginTop:32, display:'flex', alignItems:'center', gap:20, color:'var(--muted)', fontSize:17}}>
        <Tag color="var(--accent-2)">Write once</Tag>
        <span>→  Override <span className="mono" style={{color:'var(--text)'}}>GetItems</span>, <span className="mono" style={{color:'var(--text)'}}>CreateAdapter</span>, <span className="mono" style={{color:'var(--text)'}}>AddToData</span>, <span className="mono" style={{color:'var(--text)'}}>RemoveFromData</span>. That's it.</span>
      </div>

      <CornerMark>Any data</CornerMark>
    </SlideFrame>
  );
}

Object.assign(window, { Slide1, Slide2, Slide3, Slide4 });
