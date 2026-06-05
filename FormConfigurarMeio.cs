using System;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorTEF
{
    public class FormConfigurarMeio : Form
    {
        public int TempoAlertaMinutos { get; private set; }

        private NumericUpDown _nud;
        private CheckBox _chkUsar;

        public FormConfigurarMeio(MeioCaptura meio)
        {
            Text            = $"Configurar — {meio.Nome}";
            Size            = new Size(320, 170);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            Font            = new Font("Segoe UI", 9);

            var lblDesc = new Label
            {
                Text     = $"Limite de alerta para {meio.Nome}:",
                AutoSize = true,
                Location = new Point(14, 14)
            };

            _chkUsar = new CheckBox
            {
                Text     = "Usar limite individual (não usar padrão global)",
                AutoSize = true,
                Location = new Point(14, 38),
                Checked  = meio.TempoAlertaMinutos > 0
            };
            _chkUsar.CheckedChanged += (s, e) => _nud.Enabled = _chkUsar.Checked;

            _nud = new NumericUpDown
            {
                Minimum  = 1,
                Maximum  = 1440,
                Value    = meio.TempoAlertaMinutos > 0 ? meio.TempoAlertaMinutos : Config.TempoAlertaPadraoMinutos,
                Width    = 80,
                Location = new Point(14, 66),
                Enabled  = meio.TempoAlertaMinutos > 0
            };

            var lblMin = new Label
            {
                Text     = "minutos",
                AutoSize = true,
                Location = new Point(100, 68)
            };

            var btnOk = new Button
            {
                Text         = "OK",
                DialogResult = DialogResult.OK,
                Location     = new Point(140, 98),
                Size         = new Size(72, 28),
                BackColor    = Color.FromArgb(20, 60, 120),
                ForeColor    = Color.White,
                FlatStyle    = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                TempoAlertaMinutos = _chkUsar.Checked ? (int)_nud.Value : 0;
            };

            var btnCancelar = new Button
            {
                Text         = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Location     = new Point(220, 98),
                Size         = new Size(72, 28),
                FlatStyle    = FlatStyle.Flat
            };

            Controls.AddRange(new Control[] { lblDesc, _chkUsar, _nud, lblMin, btnOk, btnCancelar });
            AcceptButton = btnOk;
            CancelButton = btnCancelar;
        }
    }
}
