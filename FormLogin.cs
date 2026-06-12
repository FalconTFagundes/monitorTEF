using System;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorTEF
{
    public class FormLogin : Form
    {
        // ── resultado público ─────────────────────────────────────────────
        public string NomeUsuario   { get; private set; }
        public string PrimeiroNome  { get; private set; }
        public string CodigoUsuario { get; private set; }

        // ── controles ─────────────────────────────────────────────────────
        private TextBox _txtCodigo;
        private TextBox _txtSenha;
        private Button  _btnEntrar;
        private Label   _lblErro;
        private Label   _lblCarregando;

        // ── cores ─────────────────────────────────────────────────────────
        private static readonly Color BgForm    = Color.FromArgb(22, 24, 32);
        private static readonly Color BgInput   = Color.FromArgb(38, 42, 56);
        private static readonly Color Azul      = Color.FromArgb(61, 142, 240);
        private static readonly Color AzulHover = Color.FromArgb(90, 163, 255);
        private static readonly Color TextoP    = Color.FromArgb(220, 224, 235);
        private static readonly Color TextoS    = Color.FromArgb(120, 130, 160);
        private static readonly Color CorErro   = Color.FromArgb(220, 80, 80);

        // ─────────────────────────────────────────────────────────────────
        public FormLogin()
        {
            // ── janela ───────────────────────────────────────────────────
            Text            = "MonitorTEF — Login";
            ClientSize      = new Size(360, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterScreen;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = BgForm;
            Font            = new Font("Segoe UI", 9);
            KeyPreview      = true;

            // ── faixa azul no topo ───────────────────────────────────────
            var faixa = new Panel
            {
                BackColor = Azul,
                Location  = new Point(0, 0),
                Size      = new Size(360, 4)
            };

            // ── logo "BC" ────────────────────────────────────────────────
            var lblLogo = new Label
            {
                Text      = "BC",
                Font      = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Azul,
                BackColor = Color.FromArgb(28, 38, 60),
                TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(152, 30),
                Size      = new Size(56, 56)
            };

            // ── título ───────────────────────────────────────────────────
            var lblTitulo = new Label
            {
                Text      = "Monitor TEF",
                Font      = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextoP,
                BackColor = BgForm,
                TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(0, 100),
                Size      = new Size(360, 30)
            };

            var lblSub = new Label
            {
                Text      = "Acesse com seu código e senha",
                Font      = new Font("Segoe UI", 9),
                ForeColor = TextoS,
                BackColor = BgForm,
                TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(0, 132),
                Size      = new Size(360, 20)
            };

            // ── label Código ─────────────────────────────────────────────
            var lblCodigo = new Label
            {
                Text      = "CÓDIGO",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextoS,
                BackColor = BgForm,
                Location  = new Point(50, 172),
                Size      = new Size(260, 16)
            };

            // ── input Código ─────────────────────────────────────────────
            _txtCodigo = new TextBox
            {
                Location    = new Point(50, 190),
                Size        = new Size(260, 26),
                BackColor   = BgInput,
                ForeColor   = TextoP,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font("Segoe UI", 11),
                MaxLength   = 20
            };

            // ── label Senha ──────────────────────────────────────────────
            var lblSenha = new Label
            {
                Text      = "SENHA",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextoS,
                BackColor = BgForm,
                Location  = new Point(50, 228),
                Size      = new Size(260, 16)
            };

            // ── input Senha ──────────────────────────────────────────────
            _txtSenha = new TextBox
            {
                Location     = new Point(50, 246),
                Size         = new Size(260, 26),
                BackColor    = BgInput,
                ForeColor    = TextoP,
                BorderStyle  = BorderStyle.FixedSingle,
                Font         = new Font("Segoe UI", 11),
                PasswordChar = '●',
                MaxLength    = 20
            };

            // ── label erro ───────────────────────────────────────────────
            _lblErro = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = CorErro,
                BackColor = BgForm,
                Location  = new Point(50, 284),
                Size      = new Size(260, 18),
                Visible   = false
            };

            // ── carregando ───────────────────────────────────────────────
            _lblCarregando = new Label
            {
                Text      = "Verificando...",
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = TextoS,
                BackColor = BgForm,
                Location  = new Point(50, 284),
                Size      = new Size(260, 18),
                Visible   = false
            };

            // ── botão Entrar ─────────────────────────────────────────────
            _btnEntrar = new Button
            {
                Text      = "Entrar",
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Azul,
                FlatStyle = FlatStyle.Flat,
                Location  = new Point(50, 312),
                Size      = new Size(260, 38),
                Cursor    = Cursors.Hand
            };
            _btnEntrar.FlatAppearance.BorderSize             = 0;
            _btnEntrar.FlatAppearance.MouseOverBackColor     = AzulHover;
            _btnEntrar.Click += BtnEntrar_Click;

            // ── rodapé ───────────────────────────────────────────────────
            var lblRodape = new Label
            {
                Text      = "BigCard · TI",
                Font      = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(70, 80, 100),
                BackColor = BgForm,
                TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(0, 358),
                Size      = new Size(360, 18)
            };

            // ── adiciona todos direto no Form ─────────────────────────────
            Controls.AddRange(new Control[]
            {
                faixa,
                lblLogo, lblTitulo, lblSub,
                lblCodigo, _txtCodigo,
                lblSenha,  _txtSenha,
                _lblErro,  _lblCarregando,
                _btnEntrar, lblRodape
            });

            // Enter no código vai para senha; Enter na senha faz login
            _txtCodigo.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _txtSenha.Focus(); }
            };
            _txtSenha.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; BtnEntrar_Click(null, null); }
            };

            // Esc fecha
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
            };

            ActiveControl = _txtCodigo;
        }

        // ─────────────────────────────────────────────────────────────────
        //  AÇÃO: ENTRAR
        // ─────────────────────────────────────────────────────────────────
        private void BtnEntrar_Click(object sender, EventArgs e)
        {
            string codigo = _txtCodigo.Text.Trim();
            string senha  = _txtSenha.Text;

            _lblErro.Visible = false;

            if (string.IsNullOrEmpty(codigo))
            {
                MostrarErro("Informe o código de usuário.");
                _txtCodigo.Focus();
                return;
            }
            if (string.IsNullOrEmpty(senha))
            {
                MostrarErro("Informe a senha.");
                _txtSenha.Focus();
                return;
            }

            SetCarregando(true);

            try
            {
                string nomeCompleto;
                bool ok = BancoService.AutenticarUsuario(codigo, senha, out nomeCompleto);

                if (!ok)
                {
                    MostrarErro("Código ou senha incorretos.");
                    _txtSenha.Clear();
                    _txtSenha.Focus();
                    return;
                }

                NomeUsuario   = nomeCompleto;
                PrimeiroNome  = nomeCompleto.Split(
                    new[]{' '}, StringSplitOptions.RemoveEmptyEntries)[0];
                CodigoUsuario = codigo;

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MostrarErro($"Erro de conexão: {ex.Message}");
            }
            finally
            {
                SetCarregando(false);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        private void MostrarErro(string msg)
        {
            _lblCarregando.Visible = false;
            _lblErro.Text    = msg;
            _lblErro.Visible = true;
        }

        private void SetCarregando(bool ativo)
        {
            _btnEntrar.Enabled     = !ativo;
            _lblCarregando.Visible = ativo;
            if (ativo) _lblErro.Visible = false;  // ← só esconde ao INICIAR, não ao terminar
            Application.DoEvents();
        }
    }
}
