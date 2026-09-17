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

monitorRefreshBtn.addEventListener("click", () => loadMonitor(true));

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

  if (!data.sourceExists) {
    monitorSummary.textContent =
      "Arquivo de monitoramento não encontrado: " + (data.sourcePath || "");
    return;
  }

  if ((data.items || []).length === 0) {
    monitorSummary.textContent = "Nenhum host na lista de monitoramento.";
    return;
  }

  const when = new Date(data.checkedAt).toLocaleTimeString();
  const cacheNote = data.fromCache ? " (do cache)" : "";
  monitorSummary.textContent =
    `${data.online}/${data.total} online — verificado às ${when}${cacheNote}`;

  for (const item of data.items) {
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
      <td><button class="link-btn" data-ip="${escapeHtml(item.target)}" type="button">Ver rota</button></td>`;
    monitorBody.appendChild(tr);
  }

  // Wire the per-row "Ver rota" (tracert on demand) buttons.
  monitorBody.querySelectorAll(".link-btn").forEach((b) =>
    b.addEventListener("click", () => openTrace(b.dataset.ip)));
}

// ============== TRACERT ON-DEMAND PANEL ==============

const tracePanel = $("tracePanel");
const traceTitle = $("traceTitle");
const traceBody = $("traceBody");
$("traceClose").addEventListener("click", () => tracePanel.classList.add("hidden"));

async function openTrace(ip) {
  traceTitle.textContent = `Tracert — ${ip}`;
  traceBody.innerHTML = `<p class="status">Traçando a rota (pode levar alguns segundos)...</p>`;
  tracePanel.classList.remove("hidden");

  try {
    const response = await fetch("/api/monitor/trace?ip=" + encodeURIComponent(ip));
    if (!response.ok) {
      const err = await safeJson(response);
      throw new Error(err?.message || `Erro ${response.status}`);
    }
    const r = await response.json();
    renderTrace(r);
  } catch (e) {
    traceBody.innerHTML = `<p class="error-text">Falha: ${escapeHtml(e.message)}</p>`;
  }
}

function renderTrace(r) {
  if (r.error) {
    traceBody.innerHTML = `<p class="error-text">Erro: ${escapeHtml(r.error)}</p>`;
    return;
  }

  if (!(r.hops || []).length) {
    traceBody.innerHTML = `<p class="error-text">Sem saltos retornados.</p>`;
    return;
  }

  const reached = r.traceDestinationReached
    ? `<p class="badge ok">Destino alcançado</p>`
    : `<p class="badge fail">Destino não alcançado</p>`;

  const rows = r.hops.map((h) =>
    `<li><span class="hop-num">${h.number}</span> ${escapeHtml(h.address || "* (sem resposta)")}</li>`
  ).join("");

  traceBody.innerHTML = `${reached}<ol class="hops trace-hops">${rows}</ol>`;
}
