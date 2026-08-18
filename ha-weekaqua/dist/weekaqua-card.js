/**
 * WeekAqua Lovelace Custom Card for Home Assistant
 * Features:
 *  - Premium WPF Dark Glassmorphism UI
 *  - Real-time Channel Sliders (Red, Green, Blue, White, UV, Violet, Fan)
 *  - 100% Total Power Load Gauge
 *  - One-Click Spectrum Presets
 *  - Unlimited Steps Dynamic Schedule Editor with 24-Hour SVG Curve Visualization
 */

class WeekAquaCard extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
    this._hass = null;
    this._config = null;
    this._activeTab = 'live'; // 'live' or 'schedule'
    this._schedulePoints = [
      { time: '08:00', r: 0, g: 0, b: 0, w: 0, uv: 0, v: 0 },
      { time: '09:30', r: 20, g: 30, b: 20, w: 30, uv: 10, v: 10 },
      { time: '11:30', r: 60, g: 85, b: 60, w: 75, uv: 40, v: 30 },
      { time: '14:00', r: 70, g: 100, b: 70, w: 90, uv: 50, v: 40 },
      { time: '17:00', r: 50, g: 75, b: 50, w: 70, uv: 30, v: 20 },
      { time: '19:00', r: 25, g: 30, b: 20, w: 25, uv: 10, v: 5 },
      { time: '20:30', r: 0, g: 0, b: 4, w: 0, uv: 0, v: 0 },
      { time: '23:00', r: 0, g: 0, b: 0, w: 0, uv: 0, v: 0 },
    ];
  }

  setConfig(config) {
    if (!config.entity && !config.device_id) {
      throw new Error('Please define an entity (e.g. light.aquarium_light) or device_id.');
    }
    this._config = config;
    this._render();
  }

  set hass(hass) {
    this._hass = hass;
    this._updateState();
  }

  _calculatePower(r, g, b, w, uv = 0, v = 0) {
    return Math.min(100.0, Math.round((r * 0.41 + g * 0.42 + b * 0.49 + w * 0.08 + uv * 0.08 + v * 0.08) * 10) / 10);
  }

  _render() {
    this.shadowRoot.innerHTML = `
      <style>
        :host {
          display: block;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
          color: #F4F4F5;
        }
        .card {
          background: #18181B;
          border: 1px solid #27272A;
          border-radius: 12px;
          padding: 16px;
          box-shadow: 0 4px 20px rgba(0, 0, 0, 0.4);
        }
        .header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 14px;
        }
        .title {
          font-size: 16px;
          font-weight: 700;
          color: #93C5FD;
          display: flex;
          align-items: center;
          gap: 6px;
        }
        .tabs {
          display: flex;
          gap: 6px;
          background: #27272A;
          padding: 3px;
          border-radius: 8px;
        }
        .tab-btn {
          background: transparent;
          border: none;
          color: #A1A1AA;
          padding: 4px 10px;
          font-size: 12px;
          font-weight: 600;
          border-radius: 6px;
          cursor: pointer;
          transition: all 0.2s;
        }
        .tab-btn.active {
          background: #3B82F6;
          color: #FFF;
        }
        /* Sliders */
        .slider-group {
          margin-bottom: 8px;
        }
        .slider-row {
          display: grid;
          grid-template-columns: 70px 1fr 50px;
          align-items: center;
          gap: 10px;
          margin-bottom: 6px;
        }
        .channel-label {
          font-size: 12px;
          font-weight: 700;
        }
        input[type="range"] {
          -webkit-appearance: none;
          width: 100%;
          height: 6px;
          border-radius: 3px;
          background: #27272A;
          outline: none;
        }
        input[type="range"]::-webkit-slider-thumb {
          -webkit-appearance: none;
          width: 16px;
          height: 16px;
          border-radius: 50%;
          background: #FFF;
          cursor: pointer;
          box-shadow: 0 0 6px rgba(0, 0, 0, 0.5);
        }
        .val-badge {
          font-size: 11px;
          color: #A1A1AA;
          text-align: right;
          font-family: monospace;
        }
        /* Power Gauge */
        .gauge-wrap {
          background: #27272A;
          border-radius: 6px;
          padding: 8px 12px;
          margin-top: 10px;
          margin-bottom: 12px;
          display: flex;
          justify-content: space-between;
          align-items: center;
        }
        .gauge-bar-bg {
          flex: 1;
          height: 8px;
          background: #3F3F46;
          border-radius: 4px;
          margin: 0 10px;
          overflow: hidden;
        }
        .gauge-bar-fill {
          height: 100%;
          background: linear-gradient(90deg, #10B981, #F59E0B, #EF4444);
          width: 0%;
          transition: width 0.3s;
        }
        /* Presets */
        .presets-title {
          font-size: 12px;
          font-weight: 600;
          color: #A1A1AA;
          margin-bottom: 6px;
        }
        .preset-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(80px, 1fr));
          gap: 6px;
          margin-bottom: 12px;
        }
        .preset-btn {
          background: #27272A;
          border: 1px solid #3F3F46;
          color: #E4E4E7;
          padding: 6px;
          font-size: 11px;
          border-radius: 6px;
          cursor: pointer;
          font-weight: 500;
          transition: background 0.15s;
        }
        .preset-btn:hover {
          background: #3F3F46;
          border-color: #60A5FA;
        }
        /* Schedule Editor */
        .sched-wrap {
          display: none;
        }
        .sched-wrap.active {
          display: block;
        }
        .curve-svg {
          width: 100%;
          height: 100px;
          background: #09090B;
          border-radius: 8px;
          margin-bottom: 10px;
          border: 1px solid #27272A;
        }
        .table-wrap {
          max-height: 220px;
          overflow-y: auto;
          border: 1px solid #27272A;
          border-radius: 6px;
          margin-bottom: 10px;
        }
        table {
          width: 100%;
          border-collapse: collapse;
          font-size: 11px;
          text-align: center;
        }
        th {
          background: #27272A;
          color: #93C5FD;
          padding: 6px;
        }
        td {
          padding: 4px;
          border-bottom: 1px solid #27272A;
        }
        td input {
          width: 40px;
          background: #09090B;
          border: 1px solid #3F3F46;
          color: #FFF;
          border-radius: 4px;
          text-align: center;
          font-size: 11px;
        }
        .btn-action {
          background: #2563EB;
          color: #FFF;
          border: none;
          padding: 8px 12px;
          border-radius: 6px;
          font-weight: 600;
          font-size: 12px;
          cursor: pointer;
          width: 100%;
        }
        .btn-sm {
          padding: 2px 6px;
          font-size: 10px;
          background: #DC2626;
          color: #FFF;
          border: none;
          border-radius: 4px;
          cursor: pointer;
        }
      </style>

      <div class="card">
        <div class="header">
          <div class="title">
            <span>🐠</span> ${this._config.title || 'WeekAqua Light'}
          </div>
          <div class="tabs">
            <button class="tab-btn active" id="tab-live">Live</button>
            <button class="tab-btn" id="tab-sched">Unlimited Schedule</button>
          </div>
        </div>

        <!-- Tab 1: Live Manual Controls -->
        <div id="panel-live">
          <div class="slider-group">
            <div class="slider-row">
              <span class="channel-label" style="color: #EF4444">Red</span>
              <input type="range" id="sl-r" min="0" max="100" value="50">
              <span class="val-badge" id="txt-r">50%</span>
            </div>
            <div class="slider-row">
              <span class="channel-label" style="color: #22C55E">Green</span>
              <input type="range" id="sl-g" min="0" max="100" value="50">
              <span class="val-badge" id="txt-g">50%</span>
            </div>
            <div class="slider-row">
              <span class="channel-label" style="color: #3B82F6">Blue</span>
              <input type="range" id="sl-b" min="0" max="100" value="50">
              <span class="val-badge" id="txt-b">50%</span>
            </div>
            <div class="slider-row">
              <span class="channel-label" style="color: #F4F4F5">White</span>
              <input type="range" id="sl-w" min="0" max="100" value="50">
              <span class="val-badge" id="txt-w">50%</span>
            </div>
            <div class="slider-row">
              <span class="channel-label" style="color: #8B5CF6">UV/UVA</span>
              <input type="range" id="sl-uv" min="0" max="100" value="0">
              <span class="val-badge" id="txt-uv">0%</span>
            </div>
            <div class="slider-row">
              <span class="channel-label" style="color: #EC4899">Violet</span>
              <input type="range" id="sl-v" min="0" max="100" value="0">
              <span class="val-badge" id="txt-v">0%</span>
            </div>
          </div>

          <div class="gauge-wrap">
            <span style="font-size: 11px; font-weight: 700; color: #10B981">⚡ Total Load:</span>
            <div class="gauge-bar-bg">
              <div class="gauge-bar-fill" id="gauge-fill" style="width: 50%"></div>
            </div>
            <span style="font-size: 11px; font-weight: 700;" id="gauge-txt">50.0%</span>
          </div>

          <div class="presets-title">🎨 Spectrum Presets (One-Click)</div>
          <div class="preset-grid">
            <button class="preset-btn" data-p="GreenGrass">🌿 Green</button>
            <button class="preset-btn" data-p="RedGrass">🍁 Red Plant</button>
            <button class="preset-btn" data-p="FishMixed">🐠 Mixed</button>
            <button class="preset-btn" data-p="Shrimp">🦐 Shrimp</button>
            <button class="preset-btn" data-p="CoralAb">🪸 Coral AB+</button>
            <button class="preset-btn" data-p="Moonlight">🌙 Moonlight</button>
          </div>
        </div>

        <!-- Tab 2: Unlimited Schedule Editor -->
        <div id="panel-sched" class="sched-wrap">
          <svg class="curve-svg" id="curve-svg" viewBox="0 0 240 100" preserveAspectRatio="none">
            <path id="curve-path" d="" fill="rgba(59, 130, 246, 0.2)" stroke="#3B82F6" stroke-width="2"/>
          </svg>

          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Time</th><th>R%</th><th>G%</th><th>B%</th><th>W%</th><th>UV%</th><th>V%</th><th></th>
                </tr>
              </thead>
              <tbody id="sched-tbody"></tbody>
            </table>
          </div>

          <div style="display: flex; gap: 6px; margin-bottom: 10px;">
            <button class="preset-btn" style="flex: 1;" id="btn-add-pt">+ Add Step</button>
            <button class="preset-btn" style="flex: 1;" id="btn-auto-calc">⚡ Auto 8-Step Photoperiod</button>
          </div>

          <button class="btn-action" id="btn-save-sched">💾 Save & Sync Schedule to Home Assistant</button>
        </div>
      </div>
    `;

    this._bindEvents();
    this._renderScheduleTable();
    this._renderCurve();
  }

  _bindEvents() {
    const root = this.shadowRoot;

    // Tabs
    const tabLive = root.getElementById('tab-live');
    const tabSched = root.getElementById('tab-sched');
    const panelLive = root.getElementById('panel-live');
    const panelSched = root.getElementById('panel-sched');

    tabLive.addEventListener('click', () => {
      tabLive.classList.add('active');
      tabSched.classList.remove('active');
      panelLive.style.display = 'block';
      panelSched.classList.remove('active');
    });

    tabSched.addEventListener('click', () => {
      tabSched.classList.add('active');
      tabLive.classList.remove('active');
      panelLive.style.display = 'none';
      panelSched.classList.add('active');
      this._renderCurve();
    });

    // Sliders
    ['r', 'g', 'b', 'w', 'uv', 'v'].forEach((ch) => {
      const sl = root.getElementById(`sl-${ch}`);
      const txt = root.getElementById(`txt-${ch}`);
      sl.addEventListener('input', () => {
        txt.textContent = `${sl.value}%`;
        this._updateGauge();
      });
      sl.addEventListener('change', () => this._sendLiveSpectrum());
    });

    // Presets
    root.querySelectorAll('.preset-btn[data-p]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const pName = btn.dataset.p;
        this._applyPreset(pName);
      });
    });

    // Schedule actions
    root.getElementById('btn-add-pt').addEventListener('click', () => {
      this._schedulePoints.push({ time: '12:00', r: 50, g: 50, b: 50, w: 50, uv: 0, v: 0 });
      this._renderScheduleTable();
      this._renderCurve();
    });

    root.getElementById('btn-auto-calc').addEventListener('click', () => {
      this._schedulePoints = [
        { time: '08:00', r: 0, g: 0, b: 0, w: 0, uv: 0, v: 0 },
        { time: '09:30', r: 20, g: 30, b: 20, w: 30, uv: 10, v: 10 },
        { time: '11:30', r: 60, g: 85, b: 60, w: 75, uv: 40, v: 30 },
        { time: '14:00', r: 70, g: 100, b: 70, w: 90, uv: 50, v: 40 },
        { time: '17:00', r: 50, g: 75, b: 50, w: 70, uv: 30, v: 20 },
        { time: '19:00', r: 25, g: 30, b: 20, w: 25, uv: 10, v: 5 },
        { time: '20:30', r: 0, g: 0, b: 4, w: 0, uv: 0, v: 0 },
        { time: '23:00', r: 0, g: 0, b: 0, w: 0, uv: 0, v: 0 },
      ];
      this._renderScheduleTable();
      this._renderCurve();
    });

    root.getElementById('btn-save-sched').addEventListener('click', () => this._saveScheduleToHA());
  }

  _updateGauge() {
    const root = this.shadowRoot;
    const r = parseFloat(root.getElementById('sl-r').value);
    const g = parseFloat(root.getElementById('sl-g').value);
    const b = parseFloat(root.getElementById('sl-b').value);
    const w = parseFloat(root.getElementById('sl-w').value);
    const uv = parseFloat(root.getElementById('sl-uv').value);
    const v = parseFloat(root.getElementById('sl-v').value);

    const total = this._calculatePower(r, g, b, w, uv, v);
    root.getElementById('gauge-fill').style.width = `${total}%`;
    root.getElementById('gauge-txt').textContent = `${total.toFixed(1)}%`;
  }

  _sendLiveSpectrum() {
    const root = this.shadowRoot;
    const r = parseFloat(root.getElementById('sl-r').value);
    const g = parseFloat(root.getElementById('sl-g').value);
    const b = parseFloat(root.getElementById('sl-b').value);
    const w = parseFloat(root.getElementById('sl-w').value);
    const uv = parseFloat(root.getElementById('sl-uv').value);
    const v = parseFloat(root.getElementById('sl-v').value);

    if (this._hass) {
      this._hass.callService('weekaqua', 'set_spectrum', {
        device_id: this._config.device_id || '',
        red: r,
        green: g,
        blue: b,
        white: w,
        uv: uv,
        violet: v,
        disable_schedule: true,
      });
    }
  }

  _applyPreset(presetName) {
    if (this._hass) {
      this._hass.callService('weekaqua', 'apply_preset', {
        device_id: this._config.device_id || '',
        preset: presetName,
      });
    }
  }

  _renderScheduleTable() {
    const tbody = this.shadowRoot.getElementById('sched-tbody');
    tbody.innerHTML = '';

    this._schedulePoints.forEach((pt, idx) => {
      const tr = document.createElement('tr');
      tr.innerHTML = `
        <td><input type="text" value="${pt.time}" data-k="time" style="width:48px;"></td>
        <td><input type="number" min="0" max="100" value="${pt.r}" data-k="r"></td>
        <td><input type="number" min="0" max="100" value="${pt.g}" data-k="g"></td>
        <td><input type="number" min="0" max="100" value="${pt.b}" data-k="b"></td>
        <td><input type="number" min="0" max="100" value="${pt.w}" data-k="w"></td>
        <td><input type="number" min="0" max="100" value="${pt.uv || 0}" data-k="uv"></td>
        <td><input type="number" min="0" max="100" value="${pt.v || 0}" data-k="v"></td>
        <td><button class="btn-sm" data-del="${idx}">✕</button></td>
      `;

      tr.querySelectorAll('input').forEach((input) => {
        input.addEventListener('change', (e) => {
          const k = e.target.dataset.k;
          pt[k] = k === 'time' ? e.target.value : parseFloat(e.target.value) || 0;
          this._renderCurve();
        });
      });

      tr.querySelector('button[data-del]').addEventListener('click', () => {
        this._schedulePoints.splice(idx, 1);
        this._renderScheduleTable();
        this._renderCurve();
      });

      tbody.appendChild(tr);
    });
  }

  _renderCurve() {
    const path = this.shadowRoot.getElementById('curve-path');
    if (!path || this._schedulePoints.length === 0) return;

    const sorted = [...this._schedulePoints].sort((a, b) => {
      const tA = a.time.split(':').map(Number);
      const tB = b.time.split(':').map(Number);
      return (tA[0] * 60 + tA[1]) - (tB[0] * 60 + tB[1]);
    });

    let d = 'M 0 100 ';
    sorted.forEach((pt, i) => {
      const parts = pt.time.split(':').map(Number);
      const min = parts[0] * 60 + (parts[1] || 0);
      const x = (min / 1440) * 240;
      const power = this._calculatePower(pt.r, pt.g, pt.b, pt.w, pt.uv, pt.v);
      const y = 100 - power;
      if (i === 0) d += `L ${x} ${y} `;
      d += `L ${x} ${y} `;
    });

    d += 'L 240 100 Z';
    path.setAttribute('d', d);
  }

  _saveScheduleToHA() {
    if (this._hass) {
      this._hass.callService('weekaqua', 'set_schedule', {
        device_id: this._config.device_id || '',
        points: this._schedulePoints,
      });
      alert('✅ WeekAqua Unlimited Schedule saved and applied!');
    }
  }

  _updateState() {
    // Sync entity attributes if available
  }
}

customElements.define('weekaqua-card', WeekAquaCard);

window.customCards = window.customCards || [];
window.customCards.push({
  type: 'weekaqua-card',
  name: 'WeekAqua Aquarium Light Card',
  description: 'WPF-style Dark Spectrum Controls & Unlimited Dynamic Schedule for WeekAqua.',
});
