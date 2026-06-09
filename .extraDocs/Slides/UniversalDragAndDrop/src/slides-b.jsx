/** Slides 5-8 */

// ============ SLIDE 5: 5 DEMOS ============
function Slide5() {
  const demos = [
    {
      n:'01', title:'Inventories', desc:'Basic list‑based inventory binding. Perfect starting point.', col:'var(--accent)',
      preview: <><Grid cols={4} rows={2} size={54} gap={5} cells={[{icon:'🎒'},{icon:'🍎',count:3},null,{icon:'🧪'}, {icon:'⚔️'},null,{icon:'🪙',count:99},null]}/></>
    },
    {
      n:'02', title:'Loot / Chests', desc:'World pickup + chest UI integration without tight coupling.', col:'var(--accent-2)',
      preview: <div style={{display:'flex', gap:12, alignItems:'center'}}>
        <Grid cols={2} rows={2} size={54} gap={5} cells={[{icon:'🗝️'},{icon:'💎'},null,{icon:'📜'}]}/>
        <div style={{color:'var(--muted)', fontSize:22}}>⇄</div>
        <Grid cols={2} rows={2} size={54} gap={5} cells={[null,{icon:'🪙',count:40},{icon:'🏹'},null]}/>
      </div>
    },
    {
      n:'03', title:'Craft‑like', desc:'Slot‑indexed inventory, hotbar, and crafting flow.', col:'var(--accent-3)',
      preview: <div style={{display:'flex', flexDirection:'column', gap:8}}>
        <Grid cols={3} rows={3} size={44} gap={4} cells={[{icon:'🪵'},{icon:'🪵'},null,{icon:'🪵'},{icon:'🪵'},null,null,null,null]}/>
      </div>
    },
    {
      n:'04', title:'Trading', desc:'Cross‑model transfer, pricing rules, and bidirectional exchange.', col:'var(--green)',
      preview: <div style={{display:'flex', gap:12, alignItems:'center'}}>
        <Grid cols={2} rows={2} size={54} gap={5} cells={[{icon:'🪙',count:200},null,null,{icon:'🛡️'}]}/>
        <div style={{color:'var(--muted)', fontSize:22}}>⇄</div>
        <Grid cols={2} rows={2} size={54} gap={5} cells={[{icon:'🪙',count:80},null,{icon:'⚔️'},null]}/>
      </div>
    },
    {
      n:'05', title:'Containers', desc:'Nested inventories and container items with safe opening rules.', col:'#F5D144',
      preview: <div style={{display:'flex', gap:12}}>
        <Grid cols={2} rows={2} size={54} gap={5} cells={[{icon:'📦'},{icon:'🎒'},null,{icon:'💼'}]}/>
        <div style={{marginTop:18}}>
          <Arrow size={22} color="var(--muted)"/>
        </div>
        <Grid cols={2} rows={2} size={54} gap={5} cells={[{icon:'🧪'},{icon:'💎'},{icon:'🗝️'},null]}/>
      </div>
    },
  ];

  return (
    <SlideFrame className="bg-dots">
      <PageTag n={5}/>
      <HeaderBar
        eyebrow="04 — Examples included"
        title="5 production‑ready demo scenes."
        sub="Each demo is a complete integration pattern — copy, remix, or use as reference."
      />

      <div style={{display:'grid', gridTemplateColumns:'1fr 1fr', gap:24, flex:1}}>
        {demos.slice(0,4).map((d,i)=>(
          <div key={i} style={{
            background:'var(--panel)', border:'1px solid var(--border)',
            borderRadius:14, padding:'24px 28px',
            display:'flex', gap:24, alignItems:'center',
            boxShadow:'0 10px 20px rgba(0,0,0,0.3)',
          }}>
            <div style={{flex:1}}>
              <div style={{display:'flex', alignItems:'center', gap:12, marginBottom:10}}>
                <span className="mono" style={{fontSize:34, fontWeight:700, color:d.col, letterSpacing:'-0.03em'}}>{d.n}</span>
                <div style={{width:1, height:28, background:'var(--border-hi)'}}/>
                <span style={{fontSize:26, fontWeight:600, letterSpacing:'-0.01em'}}>{d.title}</span>
              </div>
              <div style={{fontSize:17, color:'var(--muted)', lineHeight:1.45, maxWidth:480}}>{d.desc}</div>
            </div>
            <div style={{padding:14, background:'var(--ink)', borderRadius:10, border:'1px solid var(--border)', flexShrink:0}}>
              {d.preview}
            </div>
          </div>
        ))}
        {/* 5th demo spans both columns */}
        <div style={{
          gridColumn:'1 / -1',
          background:'var(--panel)', border:'1px solid var(--border)',
          borderRadius:14, padding:'24px 28px',
          display:'flex', gap:24, alignItems:'center',
          boxShadow:'0 10px 20px rgba(0,0,0,0.3)',
        }}>
          <div style={{flex:1, display:'flex', alignItems:'center', gap:18}}>
            <span className="mono" style={{fontSize:34, fontWeight:700, color:demos[4].col, letterSpacing:'-0.03em'}}>{demos[4].n}</span>
            <div style={{width:1, height:28, background:'var(--border-hi)'}}/>
            <span style={{fontSize:26, fontWeight:600, letterSpacing:'-0.01em'}}>{demos[4].title}</span>
            <span style={{fontSize:17, color:'var(--muted)', marginLeft:14, maxWidth:620}}>{demos[4].desc}</span>
          </div>
          <div style={{padding:14, background:'var(--ink)', borderRadius:10, border:'1px solid var(--border)'}}>
            {demos[4].preview}
          </div>
        </div>
      </div>

      <CornerMark>5 demos</CornerMark>
    </SlideFrame>
  );
}

// ============ SLIDE 6: SUBSYSTEMS ============
function Slide6() {
  const groups = [
    { title:'Core inventory', col:'var(--accent)', items:[
      {name:'Drag & drop', hint:'core interaction'},
      {name:'Stacking behavior', hint:'merge and split flows'},
      {name:'Drop behavior', hint:'swapping, merging, splitting, rejecting or custom'},
      {name:'Input actions', hint:'easily customizable controls'},
      {name:'Drop Zones', hint:'world drop, redirect drop, trash, use or etc'},
    ]},
    { title:'Player UX', col:'var(--accent-2)', items:[
      {name:'Navigation', hint:'keyboard or controller support'},
      {name:'Quick transfer', hint:'auto transfer between inventories'},
      {name:'Context menu', hint:'per‑item actions'},
      {name:'Multi‑select', hint:'shift / ctrl / drag'},
      {name:'Tooltips', hint:'example included'},
    ]},
    { title:'Game rules', col:'var(--accent-3)', items:[
      {name:'CanDrag/Drop rules', hint:'mechanical checks per inventory or slot'},
      {name:'Rules presets', hint:'SO‑based rules for common cases like equipment slots or crafting grids'},
      {name:'Limited amount per slot', hint:'enforce max stack size'},
      {name:'Commit validation', hint:'extra business logic'},
      {name:'Async checks', hint:'server, DB, file'},
    ]},
    { title:'Scale up', col:'var(--green)', items:[
      {name:'Type conversions', hint:'SO ↔ runtime, model A ↔ model B'},
      {name:'Free‑form layout', hint:'create your own slot arrangements with the API'},
      {name:'Filters & sorting', hint:'custom item ordering and visibility rules'},
      {name:'Event covered', hint:'hooks for all important actions'},
      {name:'Extension points', hint:'hooks, events, and inheritance for custom features'},
    ]},
  ];

  return (
    <SlideFrame className="bg-dots">
      <PageTag n={6}/>
      <HeaderBar
        eyebrow="05 — Subsystems"
        title="Start simple. Scale without rewriting."
        sub="Begin with a working inventory, then layer in UX, rules, and advanced patterns only where your project needs them."
      />

      <div style={{display:'grid', gridTemplateColumns:'repeat(4, 1fr)', alignItems:'start', gap:22, flex:1}}>
        {groups.map((g,i)=>(
          <div key={i} style={{
            background:'var(--panel)', border:'1px solid var(--border)',
            borderRadius:14, padding:'24px 24px 22px',
            display:'flex', flexDirection:'column',
            height:'80%',
            boxShadow:'0 14px 30px -10px rgba(0,0,0,0.5)',
          }}>
            <div style={{display:'flex', alignItems:'center', gap:10, marginBottom:18}}>
              <div style={{width:10, height:10, borderRadius:3, background:g.col}}/>
              <div className="mono" style={{fontSize:13, letterSpacing:'0.14em', color:g.col, textTransform:'uppercase', fontWeight:600}}>{g.title}</div>
            </div>
            <div style={{display:'flex', flexDirection:'column', gap:12}}>
              {g.items.map((it,j)=>(
                <div key={j} style={{
                  display:'flex', alignItems:'flex-start', gap:12,
                  padding:'12px 14px',
                  background:'var(--ink)', borderRadius:10,
                  border:'1px solid var(--border)',
                }}>
                  <div style={{
                    width:22, height:22, borderRadius:6,
                    background:'var(--panel-2)', border:`1px solid ${g.col}`,
                    display:'flex', alignItems:'center', justifyContent:'center',
                    flexShrink:0, marginTop:2,
                  }}>
                    <svg width="12" height="12" viewBox="0 0 12 12">
                      <path d="M2 6l3 3 5-6" fill="none" stroke={g.col} strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"/>
                    </svg>
                  </div>
                  <div>
                    <div style={{fontSize:18, fontWeight:500, lineHeight:1.2}}>{it.name}</div>
                    <div className="mono" style={{fontSize:13, color:'var(--muted)', marginTop:3}}>{it.hint}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>

      <CornerMark>Subsystems</CornerMark>
    </SlideFrame>
  );
}

// ============ SLIDE 7: CODE ============
function Slide7() {
  const adapter = `public class ItemSOAdapter : IItemAdapter
{
    public readonly ItemSO Data;
    public ItemSOAdapter(ItemSO data) => Data = data;

    public string ItemId     => Data.GetInstanceID().ToString();
    public Sprite Icon       => Data.Icon;
    public string DisplayName => Data.ItemName;
}`;

  const binding = `public class BackpackBinding : ListInventoryDataBinding<ItemSO, ItemSOAdapter>
{
    [SerializeField] private List<ItemSO> _items;

    protected override IReadOnlyList<ItemSO> GetItems() => _items;
    protected override ItemSOAdapter CreateAdapter(ItemSO item) => new(item);
    protected override void AddToData(ItemSOAdapter a)    => _items.Add(a.Data);
    protected override void RemoveFromData(ItemSOAdapter a) => _items.Remove(a.Data);
}`;

  const Code = ({ children, lang='C#', title, highlight=[] }) => {
    const lines = children.split('\n');
    return (
      <div style={{
        background:'#0A0B0D', border:'1px solid var(--border)',
        borderRadius:12, overflow:'hidden',
        boxShadow:'0 18px 40px -12px rgba(0,0,0,0.7)',
      }}>
        <div style={{
          display:'flex', alignItems:'center', justifyContent:'space-between',
          padding:'12px 18px', borderBottom:'1px solid var(--border)',
          background:'#101114',
        }}>
          <div style={{display:'flex', alignItems:'center', gap:10}}>
            <div style={{width:10, height:10, borderRadius:999, background:'#E5484D'}}/>
            <div style={{width:10, height:10, borderRadius:999, background:'#F5A524'}}/>
            <div style={{width:10, height:10, borderRadius:999, background:'#6EE787'}}/>
            <span className="mono" style={{marginLeft:10, fontSize:13, color:'var(--muted)'}}>{title}</span>
          </div>
          <span className="mono" style={{fontSize:12, color:'var(--dim)', letterSpacing:'0.08em'}}>{lang}</span>
        </div>
        <pre className="mono" style={{
          margin:0, padding:'22px 24px', fontSize:17, lineHeight:1.55,
          color:'#D7D9DE', overflow:'auto',
        }}>
{lines.map((l, i) => (
  <div key={i} style={{
    background: highlight.includes(i) ? 'rgba(245,165,36,0.08)' : 'transparent',
    margin: highlight.includes(i) ? '0 -24px' : 0,
    padding: highlight.includes(i) ? '0 24px' : 0,
  }}>{colorize(l)}</div>
))}
        </pre>
      </div>
    );
  };

  // Crude C# colorizer
  function colorize(line) {
    const parts = [];
    const tokens = line.split(/(\s+|[<>,()=;{}.[\]])/);
    tokens.forEach((t, i) => {
      if (!t) return;
      let color = '#D7D9DE';
      if (/^(public|private|protected|class|override|readonly|new|return|void|this|null)$/.test(t)) color = '#E05CD9';
      else if (/^(string|int|bool|Sprite|ItemSO|IItemAdapter|IReadOnlyList|List|ListInventoryDataBinding|ItemSOAdapter|BackpackBinding|SerializeField)$/.test(t)) color = '#3EC6B1';
      else if (/^".*"$/.test(t)) color = '#F5D144';
      else if (/^\/\/.*/.test(t)) color = '#8A8E96';
      else if (/^(Data|Icon|ItemName|_items|ItemId|DisplayName|GetItems|CreateAdapter|AddToData|RemoveFromData|GetInstanceID|ToString|Add|Remove)$/.test(t)) color = '#F5A524';
      parts.push(<span key={i} style={{color}}>{t}</span>);
    });
    return parts;
  }

  return (
    <SlideFrame className="bg-dots">
      <PageTag n={7}/>
      <HeaderBar
        eyebrow="06 — Quick start"
        title="Two small integration files. First working inventory."
        sub="An adapter describes your item to the UI. A binding syncs events back to your data. That's the whole integration."
      />

      <div style={{display:'grid', gridTemplateColumns:'1fr 1.15fr', gap:28, flex:1, minHeight:0}}>
        <div style={{display:'flex', flexDirection:'column', gap:16}}>
          <div style={{display:'flex', alignItems:'center', gap:12}}>
            <Tag color="var(--accent-3)" filled>Step 1</Tag>
            <span style={{fontSize:22, fontWeight:500}}>Describe your item to the slot</span>
          </div>
          <Code title="Adapters/ItemSOAdapter.cs" highlight={[2,3,4,5,6,7]}>{adapter}</Code>
          <div style={{fontSize:15, color:'var(--muted)', lineHeight:1.5}}>
            <span className="mono" style={{color:'var(--accent)'}}>ItemId</span> drives stacking,
            <span className="mono" style={{color:'var(--accent)'}}> Icon</span> drives the sprite.
          </div>
        </div>

        <div style={{display:'flex', flexDirection:'column', gap:16}}>
          <div style={{display:'flex', alignItems:'center', gap:12}}>
            <Tag color="var(--accent-2)" filled>Step 2</Tag>
            <span style={{fontSize:22, fontWeight:500}}>Bind your data with one of the 3 templates</span>
          </div>
          <Code title="DataBindings/BackpackBinding.cs" highlight={[2,3,4,5,6,7]}>{binding}</Code>
          <div style={{fontSize:15, color:'var(--muted)', lineHeight:1.5}}>
            Drag, drop, stacking, swapping, events — already handled by the base class.
          </div>
        </div>
      </div>

      <CornerMark>Quick start</CornerMark>
    </SlideFrame>
  );
}

// ============ SLIDE 8: CTA / CLOSE ============
function Slide8() {
  return (
    <SlideFrame className="bg-grid" style={{padding:0}}>
      <PageTag n={8}/>

      <div style={{position:'absolute', inset:0, display:'flex', flexDirection:'column', justifyContent:'center', alignItems:'center', padding:'0 120px', textAlign:'center'}}>

        <h1 style={{fontSize:120, fontWeight:800, margin:0, letterSpacing:'-0.035em', lineHeight:0.95, maxWidth:1400, textWrap:'balance'}}>
          Build your inventory around your data model.
        </h1>
        <div style={{fontSize:28, color:'var(--muted)', margin:'42px 0 0 0', maxWidth:1100, lineHeight:1.4}}>
          <span style={{color:'var(--text)'}}>Universal Drag&Drop</span> adapts to your data model —
          not the other way around.
        </div>

        <div style={{marginTop:60, display:'flex', gap:14, flexWrap:'wrap', justifyContent:'center'}}>
          <Tag color="var(--accent)">Any data</Tag>
          <Tag color="var(--accent-2)">Stacks • Swaps • Split</Tag>
          <Tag color="var(--accent-3)">Rules • Converters • Async</Tag>
          <Tag color="var(--green)">Multi‑select • Context menu</Tag>
          <Tag color="var(--muted)">5 demos • full docs</Tag>
        </div>

        <div style={{marginTop:70, display:'flex', alignItems:'center', gap:32}}>
          <div style={{fontSize:17, color:'var(--muted)'}}>
            Supports additional integration with <span className="mono" style={{color:'var(--text)'}}>Input System</span> and <span className="mono" style={{color:'var(--text)'}}>Odin Inspector</span>
          </div>
        </div>
      </div>

      {/* Decorative corner slots */}
      <div style={{position:'absolute', left:80, bottom:80, opacity:0.45, transform:'rotate(-8deg)'}}>
        <Grid cols={3} rows={2} size={70} gap={6} cells={[{icon:'⚔️'},null,{icon:'🧪'},null,{icon:'🪙',count:42},null]}/>
      </div>
      <div style={{position:'absolute', right:80, top:160, opacity:0.45, transform:'rotate(8deg)'}}>
        <Grid cols={2} rows={2} size={70} gap={6} cells={[{icon:'💎'},{icon:'📜'},null,{icon:'🗝️'}]}/>
      </div>

      <CornerMark>Universal Drag&Drop</CornerMark>
    </SlideFrame>
  );
}

Object.assign(window, { Slide5, Slide6, Slide7, Slide8 });
