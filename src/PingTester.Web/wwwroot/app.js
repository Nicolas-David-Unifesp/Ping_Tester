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
