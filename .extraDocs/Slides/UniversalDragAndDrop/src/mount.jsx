/** Mount & tweaks wiring */

const { useState, useEffect } = React;

function App() {
  const [state, setState] = useState(window.TWEAKS);
  const [tweakOpen, setTweakOpen] = useState(false);

  useEffect(() => {
    applyPalette(state.palette);
  }, [state.palette]);

  useEffect(() => {
    function onMsg(e) {
      if (!e.data || !e.data.type) return;
      if (e.data.type === '__activate_edit_mode') setTweakOpen(true);
      if (e.data.type === '__deactivate_edit_mode') setTweakOpen(false);
    }
    window.addEventListener('message', onMsg);
    window.parent.postMessage({type: '__edit_mode_available'}, '*');
    return () => window.removeEventListener('message', onMsg);
  }, []);

  function change(k, v) {
    const next = { ...state, [k]: v };
    setState(next);
    window.TWEAKS = next;
    window.parent.postMessage({type:'__edit_mode_set_keys', edits:{[k]:v}}, '*');
    // Force re-render of slides by updating a key
    window.__rerender && window.__rerender();
  }

  return tweakOpen ? <TweaksPanel state={state} onClose={()=>setTweakOpen(false)} onChange={change}/> : null;
}

// Mount slides
const slides = [Slide1, Slide2, Slide3, Slide4, Slide5, Slide6, Slide7, Slide8];
const roots = [];
slides.forEach((S, i) => {
  const el = document.getElementById(`slide-${i+1}`);
  if (!el) return;
  const root = ReactDOM.createRoot(el);
  root.render(<S/>);
  roots.push({root, S});
});

window.__rerender = () => {
  roots.forEach(({root, S}) => root.render(<S key={Math.random()}/>));
};

// Mount tweaks controller on a dedicated node
const tweaksHost = document.createElement('div');
document.body.appendChild(tweaksHost);
ReactDOM.createRoot(tweaksHost).render(<App/>);
