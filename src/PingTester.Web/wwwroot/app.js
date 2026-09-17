"use strict";

const $ = (id) => document.getElementById(id);

const runBtn = $("run");
const statusEl = $("status");
const ipsEl = $("ips");
const csvEl = $("csv");

runBtn.addEventListener("click", runTests);

async function runTests() {
  setBusy(true, "Executando testes...");
  clearResults();

  try {
    const file = csvEl.files && csvEl.files[0];
    const response = file ? await postCsv(file) : await postTypedIps(ipsEl.value);

    if (!response.ok) {
      const err = await safeJson(response);
      throw new Error(err?.message || `Erro ${response.status}`);
    }

    const data = await response.json();
    renderInvalid(data.invalid || []);
    renderResults(data.results || []);

    const total = (data.results || []).length;
    setBusy(false, total > 0 ? `Concluído: ${total} host(s) testado(s).` : "Nenhum IP válido para testar.");
  } catch (e) {
    setBusy(false, "");
    alert("Falha ao executar: " + e.message);
  }
}

function postTypedIps(ips) {
  return fetch("/api/test", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ips }),
  });
}

function postCsv(file) {
  const form = new FormData();
  form.append("file", file);
  return fetch("/api/test/upload", { method: "POST", body: form });
}

function renderResults(results) {
  const section = $("resultsSection");
  const body = $("resultsBody");
  body.innerHTML = "";

  if (results.length === 0) {
    section.classList.add("hidden");
    return;
  }

  for (const r of results) {
    const tr = document.createElement("tr");
    if (r.error) {
      tr.innerHTML = `
        <td>${escapeHtml(r.target)}</td>
        <td colspan="6" class="error-text">Erro: ${escapeHtml(r.error)}</td>`;
      body.appendChild(tr);
      continue;
    }

    const pingBadge = r.pingReachable
      ? `<span class="badge ok">Alcançável</span>`
      : `<span class="badge fail">Sem resposta</span>`;

    const latency = r.averageLatencyMs != null ? `${r.averageLatencyMs.toFixed(1)} ms` : "—";
    const loss = `${r.lossPercentage.toFixed(0)}%`;

    const hops = (r.hops || []).length
      ? `<ol class="hops">${r.hops.map((h) => `<li>${escapeHtml(h.address)}</li>`).join("")}</ol>`
      : `<span class="error-text">${r.traceDestinationReached ? "—" : "sem rota"}</span>`;

    tr.innerHTML = `
      <td>${escapeHtml(r.target)}</td>
      <td>${pingBadge}</td>
      <td>${r.packetsSent}</td>
      <td>${r.packetsReceived}</td>
      <td>${loss}</td>
      <td>${latency}</td>
      <td>${hops}</td>`;
    body.appendChild(tr);
  }

  section.classList.remove("hidden");
}

function renderInvalid(invalid) {
  const section = $("invalidSection");
  const list = $("invalidList");
  list.innerHTML = "";

  if (invalid.length === 0) {
    section.classList.add("hidden");
    return;
  }

  for (const item of invalid) {
    const li = document.createElement("li");
    li.textContent = `${item.value} — ${item.reason}`;
    list.appendChild(li);
  }
  section.classList.remove("hidden");
}

function clearResults() {
  $("resultsSection").classList.add("hidden");
  $("invalidSection").classList.add("hidden");
  $("resultsBody").innerHTML = "";
  $("invalidList").innerHTML = "";
}

function setBusy(busy, message) {
  runBtn.disabled = busy;
  statusEl.textContent = message;
}

async function safeJson(response) {
  try { return await response.json(); } catch { return null; }
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}


// ======================= TABS =======================

let monitorLoadedOnce = false;

document.querySelectorAll(".tab").forEach((btn) => {
  btn.addEventListener("click", () => switchTab(btn.dataset.tab));
});

function switchTab(name) {
  document.querySelectorAll(".tab").forEach((b) =>
    b.classList.toggle("active", b.dataset.tab === name));
  document.querySelectorAll(".tab-panel").forEach((p) =>
    p.classList.toggle("hidden", p.id !== `tab-${name}`));

  // Opening the monitoring tab triggers the ping sweep (once; cache handles
  // subsequent opens). The "Atualizar" button forces a fresh sweep.
  if (name === "monitor" && !monitorLoadedOnce) {
    monitorLoadedOnce = true;
    loadMonitor(false);
  }
}

// ==================== MONITORING ====================

const monitorRefreshBtn = $("monitorRefresh");
const monitorSummary = $("monitorSummary");
const monitorBody = $("monitorBody");

const PAGE_SIZE = 15;
let monitorItems = [];   // full list from the server
let monitorPage = 0;     // current page index (0-based)

monitorRefreshBtn.addEventListener("click", () => loadMonitor(true));
$("pagerPrev").addEventListener("click", () => changePage(-1));
$("pagerNext").addEventListener("click", () => changePage(1));

async function loadMonitor(forceRefresh) {
  monitorRefreshBtn.disabled = true;
  monitorSummary.textContent = forceRefresh ? "Atualizando..." : "Verificando os hosts...";

  try {
    const url = "/api/monitor" + (forceRefresh ? "?refresh=true" : "");
    const response = await fetch(url);
    if (!response.ok) throw new Error(`Erro ${response.status}`);

    const data = await response.json();
    renderMonitor(data);
  } catch (e) {
    monitorSummary.textContent = "Falha ao carregar: " + e.message;
  } finally {
    monitorRefreshBtn.disabled = false;
  }
}

function renderMonitor(data) {
  monitorBody.innerHTML = "";
  monitorItems = data.items || [];
  monitorPage = 0;

  if (!data.sourceExists) {
    monitorSummary.textContent =
      "Arquivo de monitoramento não encontrado: " + (data.sourcePath || "");
    renderPager();
    return;
  }

  if (monitorItems.length === 0) {
    monitorSummary.textContent = "Nenhum host na lista de monitoramento.";
    renderPager();
    return;
  }

  const when = new Date(data.checkedAt).toLocaleTimeString();
  const cacheNote = data.fromCache ? " (do cache)" : "";
  monitorSummary.textContent =
    `${data.online}/${data.total} online — verificado às ${when}${cacheNote}`;

  renderMonitorPage();
}

function renderMonitorPage() {
  monitorBody.innerHTML = "";

  const start = monitorPage * PAGE_SIZE;
  const pageItems = monitorItems.slice(start, start + PAGE_SIZE);

  for (const item of pageItems) {
    const tr = document.createElement("tr");

    const statusBadge = item.online
      ? `<span class="badge ok">● Online</span>`
      : `<span class="badge fail">● Offline</span>`;

    const latency = item.averageLatencyMs != null ? `${item.averageLatencyMs.toFixed(1)} ms` : "—";
    const loss = `${item.lossPercentage.toFixed(0)}%`;

    tr.innerHTML = `
      <td>${statusBadge}</td>
      <td>${escapeHtml(item.target)}</td>
      <td>${latency}</td>
      <td>${loss}</td>
      <td><button class="link-btn" data-ip="${escapeHtml(item.target)}" type="button">Ver detalhes</button></td>`;
    monitorBody.appendChild(tr);
  }

  // Wire the per-row "Ver detalhes" (full ping + tracert on demand) buttons.
  monitorBody.querySelectorAll(".link-btn").forEach((b) =>
    b.addEventListener("click", () => openDetail(b.dataset.ip)));

  renderPager();
}

function renderPager() {
  const pager = $("monitorPager");
  const totalPages = Math.max(1, Math.ceil(monitorItems.length / PAGE_SIZE));

  if (monitorItems.length <= PAGE_SIZE) {
    pager.classList.add("hidden");
    return;
  }
  pager.classList.remove("hidden");

  $("pagerInfo").textContent = `Página ${monitorPage + 1} de ${totalPages}`;
  $("pagerPrev").disabled = monitorPage === 0;
  $("pagerNext").disabled = monitorPage >= totalPages - 1;
}

function changePage(delta) {
  const totalPages = Math.ceil(monitorItems.length / PAGE_SIZE);
  const next = monitorPage + delta;
  if (next < 0 || next >= totalPages) return;
  monitorPage = next;
  renderMonitorPage();
}

// ======= DETAIL ON-DEMAND PANEL (full ping + tracert) =======

const tracePanel = $("tracePanel");
const traceTitle = $("traceTitle");
const traceBody = $("traceBody");
$("traceClose").addEventListener("click", () => tracePanel.classList.add("hidden"));

async function openDetail(ip) {
  traceTitle.textContent = `Detalhes — ${ip}`;
  traceBody.innerHTML = `<p class="status">Executando ping e tracert (pode levar alguns segundos)...</p>`;
  tracePanel.classList.remove("hidden");

  try {
    const response = await fetch("/api/monitor/detail?ip=" + encodeURIComponent(ip));
    if (!response.ok) {
      const err = await safeJson(response);
      throw new Error(err?.message || `Erro ${response.status}`);
    }
    const r = await response.json();
    renderDetail(r);
  } catch (e) {
    traceBody.innerHTML = `<p class="error-text">Falha: ${escapeHtml(e.message)}</p>`;
  }
}

function renderDetail(detail) {
  // The detail endpoint returns { result, rawPingOutput, rawTraceOutput }.
  const r = detail.result || detail;

  // Always build the raw console blocks (below), even on error/offline —
  // that's exactly when the user wants to see what the CMD returned.
  const rawHtml =
    rawBlock("Saída do ping", detail.rawPingOutput) +
    rawBlock("Saída do tracert", detail.rawTraceOutput);

  if (r.error) {
    // Show the error, but STILL show whatever raw output we captured.
    traceBody.innerHTML =
      `<p class="error-text">Erro: ${escapeHtml(r.error)}</p>` + rawHtml;
    wireCopyButtons();
    return;
  }

  // --- Ping summary (full 4-packet ping) ---
  const pingBadge = r.pingReachable
    ? `<span class="badge ok">Alcançável</span>`
    : `<span class="badge fail">Sem resposta</span>`;
  const latency = r.averageLatencyMs != null ? `${r.averageLatencyMs.toFixed(1)} ms` : "—";
  const pingHtml = `
    <h4>Ping</h4>
    <p>${pingBadge}</p>
    <ul class="detail-stats">
      <li>Enviados: <strong>${r.packetsSent}</strong></li>
      <li>Recebidos: <strong>${r.packetsReceived}</strong></li>
      <li>Perda: <strong>${r.lossPercentage.toFixed(0)}%</strong></li>
      <li>Latência média: <strong>${latency}</strong></li>
    </ul>`;

  // --- Tracert summary ---
  let traceHtml = `<h4>Tracert</h4>`;
  if (!(r.hops || []).length) {
    traceHtml += `<p class="error-text">Sem saltos retornados.</p>`;
  } else {
    const reached = r.traceDestinationReached
      ? `<p class="badge ok">Destino alcançado</p>`
      : `<p class="badge fail">Destino não alcançado</p>`;
    const rows = r.hops.map((h) =>
      `<li><span class="hop-num">${h.number}</span> ${escapeHtml(h.address || "* (sem resposta)")}</li>`
    ).join("");
    traceHtml += `${reached}<ol class="hops trace-hops">${rows}</ol>`;
  }

  // --- Verbatim console output (copy/paste) --- (rawHtml built at top)
  traceBody.innerHTML = pingHtml + traceHtml + rawHtml;
  wireCopyButtons();
}

function wireCopyButtons() {
  traceBody.querySelectorAll(".copy-btn").forEach((btn) => {
    btn.addEventListener("click", () => copyText(btn));
  });
}

function rawBlock(title, text) {
  const hasText = text != null && String(text).trim().length > 0;

  // When there is text, show it with a Copy button. When empty, show a small
  // note instead of hiding the block entirely (so it's never mysteriously
  // blank — e.g. a host that returned nothing at all).
  const bodyHtml = hasText
    ? `<button class="copy-btn link-btn" type="button">Copiar</button>`
    : "";
  const pre = hasText
    ? `<pre class="raw-output">${escapeHtml(text)}</pre>`
    : `<p class="status">(sem saída capturada)</p>`;

  return `
    <div class="raw-block">
      <div class="raw-head">
        <h4>${escapeHtml(title)}</h4>
        ${bodyHtml}
      </div>
      ${pre}
    </div>`;
}

async function copyText(btn) {
  const pre = btn.closest(".raw-block").querySelector(".raw-output");
  const text = pre.textContent;
  try {
    await navigator.clipboard.writeText(text);
    const original = btn.textContent;
    btn.textContent = "Copiado!";
    setTimeout(() => { btn.textContent = original; }, 1500);
  } catch {
    // Fallback: select the text so the user can copy manually.
    const range = document.createRange();
    range.selectNodeContents(pre);
    const sel = window.getSelection();
    sel.removeAllRanges();
    sel.addRange(range);
  }
}
