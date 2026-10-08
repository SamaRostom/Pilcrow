// Pilcrow editor.
//
// The template document is the single contract between this canvas and the
// server renderer: the same JSON that draws blocks here is what produces the
// PDF. Nothing here knows how a PDF is made, and the renderer knows nothing
// about the DOM.

const $ = (sel, root = document) => root.querySelector(sel);

// ---------------------------------------------------------------- session --

const Session = {
  access: null,
  get refresh() { return localStorage.getItem('pilcrow.refresh'); },
  set refresh(v) {
    if (v) localStorage.setItem('pilcrow.refresh', v);
    else localStorage.removeItem('pilcrow.refresh');
  },
  store({ accessToken, refreshToken }) {
    this.access = accessToken;
    this.refresh = refreshToken;
  },
  clear() {
    this.access = null;
    this.refresh = null;
  }
};

// One place that knows about tokens. A 401 triggers a single refresh attempt
// and replays the request; a second failure drops the session.
async function api(path, { method = 'GET', body, raw = false, retry = true } = {}) {
  const headers = {};
  if (Session.access) headers.Authorization = `Bearer ${Session.access}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const res = await fetch(path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body)
  });

  if (res.status === 401 && retry && Session.refresh) {
    const refreshed = await tryRefresh();
    if (refreshed) return api(path, { method, body, raw, retry: false });
    signOut();
    throw new Error('Session expired. Sign in again.');
  }

  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(text?.slice(0, 200) || `Request failed (${res.status})`);
  }

  if (raw) return res;
  if (res.status === 204) return null;

  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

async function tryRefresh() {
  try {
    const res = await fetch('/auth/refresh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: Session.refresh })
    });
    if (!res.ok) return false;
    Session.store(await res.json());
    return true;
  } catch {
    return false;
  }
}

// ------------------------------------------------------------------- state --

const state = {
  templates: [],
  currentId: null,
  doc: null,          // { page: {...}, blocks: [...] }
  dirty: false,
  data: {}
};

const DEFAULT_DOC = () => ({ page: { size: 'A4', margin: 40 }, blocks: [] });

const BLOCK_DEFAULTS = {
  heading: () => ({ type: 'heading', text: 'Heading' }),
  text:    () => ({ type: 'text', text: 'Body text. Use {{field}} to insert a value.' }),
  divider: () => ({ type: 'divider' }),
  spacer:  () => ({ type: 'spacer', height: 24 }),
  table:   () => ({
    type: 'table',
    source: 'items',
    columns: [
      { label: 'Description', field: 'desc' },
      { label: 'Qty', field: 'qty' },
      { label: 'Price', field: 'price' }
    ]
  })
};

// ------------------------------------------------------------------- toasts --

function toast(message, kind = '') {
  const el = document.createElement('div');
  el.className = `toast${kind ? ' is-' + kind : ''}`;
  el.textContent = message;
  $('#toasts').append(el);
  setTimeout(() => el.remove(), 3600);
}

// -------------------------------------------------------------------- gate --

let gateMode = 'login';

function showGate() {
  $('#gate').hidden = false;
  $('#app').hidden = true;
}

function showApp() {
  $('#gate').hidden = true;
  $('#app').hidden = false;
}

$('#gate-toggle').addEventListener('click', () => {
  gateMode = gateMode === 'login' ? 'signup' : 'login';
  const signup = gateMode === 'signup';
  $('#gate-submit').textContent = signup ? 'Create account' : 'Sign in';
  $('#gate-toggle').textContent = signup ? 'I already have an account' : 'Create an account';
  $('#gate-name-field').hidden = !signup;
  $('#gate-password').autocomplete = signup ? 'new-password' : 'current-password';
  $('#gate-error').hidden = true;
});

$('#gate-form').addEventListener('submit', async (e) => {
  e.preventDefault();

  const email = $('#gate-email').value.trim();
  const password = $('#gate-password').value;
  const err = $('#gate-error');
  err.hidden = true;

  if (!email || password.length < 8) {
    err.textContent = 'Enter an email and a password of at least 8 characters.';
    err.hidden = false;
    return;
  }

  const btn = $('#gate-submit');
  btn.disabled = true;

  try {
    const body = gateMode === 'signup'
      ? { email, password, fullName: $('#gate-name').value.trim() || null }
      : { email, password };

    const res = await fetch(`/auth/${gateMode}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });

    if (!res.ok) {
      err.textContent = (await res.text()) || 'Could not sign in.';
      err.hidden = false;
      return;
    }

    Session.store(await res.json());
    showApp();
    await loadTemplates();
  } catch {
    err.textContent = 'Could not reach the server.';
    err.hidden = false;
  } finally {
    btn.disabled = false;
  }
});

function signOut() {
  const token = Session.refresh;
  if (token) {
    fetch('/auth/logout', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: token })
    }).catch(() => {});
  }
  Session.clear();
  state.currentId = null;
  state.doc = null;
  showGate();
}

$('#btn-signout').addEventListener('click', signOut);

// --------------------------------------------------------------- templates --

async function loadTemplates() {
  state.templates = await api('/templates');
  renderTemplateList();

  if (!state.currentId && state.templates.length) {
    await openTemplate(state.templates[0].id);
  }
}

function renderTemplateList() {
  const list = $('#template-list');
  list.replaceChildren();

  $('#template-empty').hidden = state.templates.length > 0;

  for (const t of state.templates) {
    const li = document.createElement('li');
    const btn = document.createElement('button');
    btn.className = 'template-item';
    btn.setAttribute('aria-current', String(t.id === state.currentId));

    const name = document.createElement('span');
    name.className = 'template-name';
    name.textContent = t.name;

    const sub = document.createElement('span');
    sub.className = 'template-sub';
    sub.textContent = `Version ${t.currentVersion} · ${relativeTime(t.updatedAt)}`;

    btn.append(name, sub);
    btn.addEventListener('click', () => openTemplate(t.id));
    li.append(btn);
    list.append(li);
  }
}

async function openTemplate(id) {
  if (state.dirty && !confirm('Discard unsaved changes?')) return;

  try {
    const detail = await api(`/templates/${id}`);
    state.currentId = detail.id;
    state.doc = normaliseDoc(detail.doc);
    state.dirty = false;

    $('#doc-name').value = detail.name;
    $('#doc-name').disabled = false;
    $('#btn-render').disabled = false;

    setMeta(`Version ${detail.currentVersion}`);
    renderTemplateList();
    renderBlocks();
    setDirty(false);
  } catch (e) {
    toast(e.message, 'warn');
  }
}

$('#btn-new').addEventListener('click', async () => {
  const name = prompt('Name this template');
  if (!name?.trim()) return;

  try {
    const created = await api('/templates', {
      method: 'POST',
      body: { name: name.trim(), doc: DEFAULT_DOC() }
    });
    state.templates.unshift(created);
    await openTemplate(created.id);
    toast('Template created');
  } catch (e) {
    toast(e.message, 'warn');
  }
});

// ------------------------------------------------------------------ canvas --

function normaliseDoc(doc) {
  const d = doc && typeof doc === 'object' ? doc : DEFAULT_DOC();
  if (!Array.isArray(d.blocks)) d.blocks = [];
  if (!d.page) d.page = { size: 'A4', margin: 40 };
  return d;
}

function renderBlocks() {
  const host = $('#blocks');
  host.replaceChildren();

  const blocks = state.doc?.blocks ?? [];
  $('#page-empty').hidden = blocks.length > 0;

  blocks.forEach((block, index) => host.append(buildBlock(block, index)));
}

function buildBlock(block, index) {
  const li = document.createElement('li');
  li.className = `block block-${block.type}`;
  li.dataset.index = String(index + 1);
  li.dataset.position = String(index);

  li.append(buildBody(block, index), buildTools(index));
  wireDragTarget(li, index);
  return li;
}

function buildBody(block, index) {
  if (block.type === 'divider') {
    const hr = document.createElement('div');
    hr.className = 'rule';
    return hr;
  }

  if (block.type === 'spacer') {
    const gap = document.createElement('div');
    gap.className = 'gap';
    return gap;
  }

  if (block.type === 'table') {
    return buildTablePreview(block);
  }

  // Heading and paragraph are edited in place.
  const area = document.createElement('textarea');
  area.className = 'block-body';
  area.rows = 1;
  area.value = block.text ?? '';
  area.setAttribute('aria-label', `${block.type} block`);

  const grow = () => {
    area.style.height = 'auto';
    area.style.height = `${area.scrollHeight}px`;
  };

  area.addEventListener('input', () => {
    state.doc.blocks[index].text = area.value;
    setDirty(true);
    grow();
  });

  requestAnimationFrame(grow);
  return area;
}

function buildTablePreview(block) {
  const wrap = document.createElement('div');
  const table = document.createElement('table');

  const head = document.createElement('tr');
  for (const col of block.columns ?? []) {
    const th = document.createElement('th');
    th.textContent = col.label ?? '';
    head.append(th);
  }
  table.append(head);

  // One sample row, so the shape is visible without pretending to hold data.
  const row = document.createElement('tr');
  for (const col of block.columns ?? []) {
    const td = document.createElement('td');
    td.textContent = `{{${col.field ?? ''}}}`;
    row.append(td);
  }
  table.append(row);

  const note = document.createElement('p');
  note.className = 'source';
  note.textContent = `Rows come from "${block.source ?? 'items'}" in your data.`;

  wrap.append(table, note);
  return wrap;
}

function buildTools(index) {
  const tools = document.createElement('div');
  tools.className = 'block-tools';

  const drag = document.createElement('button');
  drag.className = 'tool';
  drag.type = 'button';
  drag.draggable = true;
  drag.textContent = '⠿';
  drag.title = 'Drag to reorder';
  drag.setAttribute('aria-label', 'Drag to reorder');
  drag.addEventListener('dragstart', (e) => {
    e.dataTransfer.setData('text/plain', String(index));
    e.dataTransfer.effectAllowed = 'move';
    drag.closest('.block')?.classList.add('is-dragging');
  });
  drag.addEventListener('dragend', () => {
    document.querySelectorAll('.block').forEach(b =>
      b.classList.remove('is-dragging', 'is-over'));
  });

  const up = toolButton('↑', 'Move up', () => move(index, index - 1));
  const down = toolButton('↓', 'Move down', () => move(index, index + 1));
  const del = toolButton('✕', 'Delete block', () => {
    state.doc.blocks.splice(index, 1);
    setDirty(true);
    renderBlocks();
  });

  tools.append(drag, up, down, del);
  return tools;
}

function toolButton(label, title, onClick) {
  const b = document.createElement('button');
  b.className = 'tool';
  b.type = 'button';
  b.textContent = label;
  b.title = title;
  b.setAttribute('aria-label', title);
  b.addEventListener('click', onClick);
  return b;
}

function wireDragTarget(li, index) {
  li.addEventListener('dragover', (e) => {
    e.preventDefault();
    li.classList.add('is-over');
  });
  li.addEventListener('dragleave', () => li.classList.remove('is-over'));
  li.addEventListener('drop', (e) => {
    e.preventDefault();
    li.classList.remove('is-over');
    const from = Number(e.dataTransfer.getData('text/plain'));
    if (Number.isInteger(from)) move(from, index);
  });
}

function move(from, to) {
  const blocks = state.doc.blocks;
  if (to < 0 || to >= blocks.length || from === to) return;
  const [moved] = blocks.splice(from, 1);
  blocks.splice(to, 0, moved);
  setDirty(true);
  renderBlocks();
}

document.querySelectorAll('[data-add]').forEach(btn => {
  btn.addEventListener('click', () => {
    if (!state.doc) { toast('Open a template first'); return; }
    state.doc.blocks.push(BLOCK_DEFAULTS[btn.dataset.add]());
    setDirty(true);
    renderBlocks();
  });
});

// -------------------------------------------------------------------- save --

function setDirty(dirty) {
  state.dirty = dirty;
  $('#btn-save').disabled = !dirty;
  if (dirty) setMeta('Unsaved changes');
}

function setMeta(text) {
  $('#doc-meta').textContent = text;
}

async function save() {
  if (!state.currentId || !state.dirty) return;

  $('#btn-save').disabled = true;
  try {
    const summary = await api(`/templates/${state.currentId}`, {
      method: 'PUT',
      body: { doc: state.doc }
    });

    const idx = state.templates.findIndex(t => t.id === summary.id);
    if (idx >= 0) state.templates[idx] = summary;

    state.dirty = false;
    setMeta(`Version ${summary.currentVersion} · saved`);
    renderTemplateList();
  } catch (e) {
    toast(e.message, 'warn');
    $('#btn-save').disabled = false;
  }
}

$('#btn-save').addEventListener('click', save);

document.addEventListener('keydown', (e) => {
  if ((e.metaKey || e.ctrlKey) && e.key === 's') {
    e.preventDefault();
    save();
  }
});

window.addEventListener('beforeunload', (e) => {
  if (state.dirty) { e.preventDefault(); e.returnValue = ''; }
});

$('#doc-name').addEventListener('change', () => {
  // Renaming needs its own endpoint; until then the name is read-only server-side.
  toast('Renaming is not available yet');
  const current = state.templates.find(t => t.id === state.currentId);
  if (current) $('#doc-name').value = current.name;
});

// -------------------------------------------------------------------- data --

document.querySelectorAll('.tab').forEach(tab => {
  tab.addEventListener('click', () => {
    document.querySelectorAll('.tab').forEach(t => {
      const on = t === tab;
      t.classList.toggle('is-active', on);
      t.setAttribute('aria-selected', String(on));
    });
    document.querySelectorAll('.tab-body').forEach(body => {
      body.hidden = body.dataset.panel !== tab.dataset.tab;
    });
  });
});

const dataField = $('#data-json');
dataField.value = JSON.stringify({
  number: 'INV-001',
  customer: 'Acme Ltd',
  items: [
    { desc: 'Consulting', qty: 10, price: 500 },
    { desc: 'Support', qty: 1, price: 1200 }
  ]
}, null, 2);

dataField.addEventListener('input', () => {
  const err = $('#data-error');
  try {
    state.data = JSON.parse(dataField.value || '{}');
    err.hidden = true;
  } catch (e) {
    err.textContent = `Not valid JSON: ${e.message}`;
    err.hidden = false;
  }
});

state.data = JSON.parse(dataField.value);

// ------------------------------------------------------------------ render --

const sheet = $('#render-sheet');

$('#render-close').addEventListener('click', () => { sheet.hidden = true; });

sheet.addEventListener('click', (e) => {
  if (e.target === sheet) sheet.hidden = true;
});

$('#btn-render').addEventListener('click', async () => {
  if (!state.currentId) return;

  if (!$('#data-error').hidden) {
    toast('Fix the data before rendering', 'warn');
    return;
  }

  if (state.dirty) await save();

  openSheet('Queued', 15);

  try {
    const job = await api('/renders', {
      method: 'POST',
      body: {
        templateId: state.currentId,
        data: state.data,
        idempotencyKey: crypto.randomUUID()
      }
    });

    await pollJob(job.jobId);
  } catch (e) {
    setSheet(e.message, 100);
    toast(e.message, 'warn');
  }
});

function openSheet(status, percent) {
  sheet.hidden = false;
  $('#render-download').hidden = true;
  setSheet(status, percent);
}

function setSheet(status, percent) {
  $('#render-status').textContent = status;
  $('#render-bar').style.width = `${percent}%`;
}

async function pollJob(jobId) {
  const started = Date.now();

  while (Date.now() - started < 120_000) {
    const job = await api(`/renders/${jobId}`);

    if (job.status === 'done') {
      setSheet('Ready', 100);
      await attachDownload(jobId);
      return;
    }

    if (job.status === 'failed' || job.status === 'cancelled') {
      setSheet(`Failed: ${job.errorCode ?? 'unknown error'}`, 100);
      return;
    }

    setSheet(job.status === 'running' ? 'Rendering' : 'Queued',
             job.status === 'running' ? 70 : 30);

    await new Promise(r => setTimeout(r, 800));
  }

  setSheet('Still running. Check back shortly.', 100);
}

// The download endpoint needs the bearer token, so the bytes are fetched and
// handed to the browser as an object URL rather than linked directly.
async function attachDownload(jobId) {
  const res = await api(`/renders/${jobId}/download`, { raw: true });
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);

  const name = (state.templates.find(t => t.id === state.currentId)?.name ?? 'document')
    .replace(/[^\w-]+/g, '-').toLowerCase();

  const link = $('#render-download');
  link.href = url;
  link.download = `${name}.pdf`;
  link.hidden = false;
  link.onclick = () => setTimeout(() => URL.revokeObjectURL(url), 1000);
}

// ------------------------------------------------------------------ helpers --

function relativeTime(iso) {
  const then = new Date(iso).getTime();
  const mins = Math.round((Date.now() - then) / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.round(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  return new Date(iso).toLocaleDateString();
}

// -------------------------------------------------------------------- boot --

(async function boot() {
  if (Session.refresh && await tryRefresh()) {
    showApp();
    try {
      await loadTemplates();
      return;
    } catch {
      Session.clear();
    }
  }
  showGate();
})();