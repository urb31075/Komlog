// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WorkScriptItemWindow.xaml.cs" company="URB">
//  All Right Reserved
// </copyright>
// <summary>
//   Interaction logic for WorkScriptItemWindow.xaml
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Komlog
{
    using System.Windows;

    /// <summary>
    /// Interaction logic for WorkScriptItemWindow.xaml
    /// </summary>
    public partial class WorkScriptItemWindow
    {
        /// <summary>
        /// The save on exit.
        /// </summary>
        private bool saveOnExit;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkScriptItemWindow"/> class.
        /// </summary>
        public WorkScriptItemWindow()
        {
            InitializeComponent();
            this.saveOnExit = false;
        }

        /// <summary>
        /// The run.
        /// </summary>
        /// <param name="owner">
        /// The owner.
        /// </param>
        /// <param name="mode">
        /// The mode.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public bool Run(Window owner)
        {
            this.Owner = owner;
            this.ShowDialog();
            return this.saveOnExit;
        }

        /// <summary>
        /// The cancel button click.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// The save button click.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void AbbplayButtonClick(object sender, RoutedEventArgs e)
        {
            this.saveOnExit = true;
            this.Close();
        }
    }
}
