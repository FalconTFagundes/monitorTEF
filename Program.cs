using System;
using System.Windows.Forms;

namespace MonitorTEF
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Abre tela de login
            using (var login = new FormLogin())
            {
                if (login.ShowDialog() != DialogResult.OK)
                    return; // usuário fechou sem logar

                // Login OK — abre o monitor com o nome do operador
                Application.Run(new FormPrincipal(
                    login.NomeUsuario,
                    login.PrimeiroNome,
                    login.CodigoUsuario));
            }
        }
    }
}
