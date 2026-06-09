using System;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorTEF
{
    /// <summary>
    /// Permite configurar uma tolerância individual (%) para um meio específico.
    /// Deixar em 0 = usa a tolerância global definida no rodapé do FormPrincipal.
    /// </summary>
    public class FormConfigurarMeio : Form
    {
        public int ToleranciaPercent { get; private set; }

        private NumericUpDown _nud;
        private CheckBox      _chkUsar;

        public FormConfigurarMeio(MeioCaptura meio)
        {
            Text            = $"Configurar — {meio.Nome}";
            Size            = new Size(400, 240);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            Font            = new Font("Segoe UI", 9);

            // ── explicação do cálculo dinâmico ────────────────────────────
            var lblInfo = new Label
            {
                Text = "O limite de alerta é calculado automaticamente:\n" +
                       "  média = período / total de transações\n" +
                       "  alerta quando: ocioso > média × tolerância%",
                Location  = new Point(14, 12),
                Size      = new Size(370, 54),
                ForeColor = Color.FromArgb(60, 80, 120)
            };

            // ── métricas atuais ───────────────────────────────────────────
            string mediaStr = meio.MediaIntervaloMinutos > 0
                ? $"{meio.MediaIntervaloMinutos:F1} min"
                : "sem dados suficientes";
            string limiteStr = meio.MediaIntervaloMinutos > 0
                ? $"{meio.MediaIntervaloMinutos * meio.ToleranciaEfetivaPercent / 100.0:F1} min"
                : "—";

            var lblMetricas = new Label
            {
                Text      = $"Média atual do meio: {mediaStr}   |   " +
                             $"Alerta atual em: {limiteStr}   |   " +
                             $"Tolerância efetiva: {meio.ToleranciaEfetivaPercent}%",
                Location  = new Point(14, 70),
                Size      = new Size(370, 20),
                Font      = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.Gray
            };

            // ── checkbox ──────────────────────────────────────────────────
            _chkUsar = new CheckBox
            {
                Text     = "Usar tolerância individual (ignorar o global)",
                AutoSize = true,
                Location = new Point(14, 100),
                Checked  = meio.ToleranciaIndividualPercent > 0
            };
            _chkUsar.CheckedChanged += (s, e) => _nud.Enabled = _chkUsar.Checked;

            // ── input ─────────────────────────────────────────────────────
            _nud = new NumericUpDown
            {
                Minimum  = 100,
                Maximum  = 999,
                Value    = meio.ToleranciaIndividualPercent > 0
                           ? meio.ToleranciaIndividualPercent
                           : Config.ToleranciaGlobalPercent,
                Width    = 74,
                Location = new Point(14, 128),
                Enabled  = meio.ToleranciaIndividualPercent > 0
            };

            var lblPct = new Label
            {
                Text     = "%  (100–999%)",
                AutoSize = true,
                Location = new Point(94, 130)
            };

            // ── botões ────────────────────────────────────────────────────
            var btnOk = new Button
            {
                Text         = "OK",
                DialogResult = DialogResult.OK,
                Location     = new Point(200, 165),
                Size         = new Size(80, 28),
                BackColor    = Color.FromArgb(20, 60, 120),
                ForeColor    = Color.White,
                FlatStyle    = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                ToleranciaPercent = _chkUsar.Checked ? (int)_nud.Value : 0;
            };

            var btnCancelar = new Button
            {
                Text         = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Location     = new Point(290, 165),
                Size         = new Size(80, 28),
                FlatStyle    = FlatStyle.Flat
            };

            Controls.AddRange(new Control[]
            {
                lblInfo, lblMetricas, _chkUsar, _nud, lblPct, btnOk, btnCancelar
            });
            AcceptButton = btnOk;
            CancelButton = btnCancelar;
        }
    }
}
