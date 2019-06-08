﻿
namespace Konvolucio.MCEL181123
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.Text;
    using System.Threading;
    using System.Diagnostics;
    using Properties;
    using Events;
    using Database;


    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            new App();
        }
    }

    public interface IApp
    {

    }

    public class App : IApp
    {
        public static SynchronizationContext SyncContext = null;


        IMainForm _mainForm;
        Explorer _explorer;
        IIoService _ioService;

        private readonly TreeNode _startTreeNode;

        public App()
        { 
            /*** Main Form ***/
            _mainForm = new MainForm();
            _mainForm.Text = AppConstants.SoftwareTitle + " - " + Application.ProductVersion;
            _mainForm.Shown += MainForm_Shown;
            _mainForm.FormClosing += MainForm_FormClosing;
            _mainForm.FormClosed += new FormClosedEventHandler(MainForm_FormClosed);

            /*** Explorer ***/
            _explorer = new Explorer();

            /*** IoService ***/
            _ioService = new IoService(_explorer);
            _ioService.Started += IoService_Started;
            _ioService.Stopped += IoService_Stopped;

            /*** TimerService ***/
            TimerService.Instance.Interval = 1000;

            #region MenuBar    
            /* Menu Bar */
            var configMenu = new ToolStripMenuItem("Config");
            configMenu.DropDown.Items.AddRange(
                new ToolStripItem[]
                {
                    new Commands.OptionsCommand(this)
                });

            var helpMenu = new ToolStripMenuItem("Help");
            helpMenu.DropDown.Items.AddRange(
                 new ToolStripItem[]
                 {
                     new Commands.HowIsWorkingCommand(),
                    // new Commands.UpdatesCommands(),
                 });

            var runMenu = new ToolStripMenuItem("Run");
            runMenu.DropDown.Items.AddRange(
            new ToolStripItem[]
               {
                     new Commands.PlayCommand(_ioService),
                     new Commands.StopCommand(_ioService),
                     new Commands.ResetCommand()
               });

            _mainForm.MenuBar = new ToolStripItem[]
                {
                   // configMenu,
                    runMenu,
                    //viewMenu,
                    helpMenu,
                };
            #endregion

            #region SendView

            var sendView = _mainForm.SendView;
            sendView.Signals = CanDb.Instance.Signals.Where(n => n.Message.Node.Name == NodeCollection.NODE_PC ).Select(n=>n.Name).ToArray();
            sendView.SelectedSignalChanged += (o, s) =>
            {
                sendView.Value = CanDb.Instance.Signals.FirstOrDefault(n => n.Name == sendView.SelectedSignal).DefaultValue;
            };
            sendView.Send += (o, s) =>
            {
              var msg = CanDb.MakeMessage
                (
                    nodeId: 0x05,
                    signal: CanDb.Instance.Signals.FirstOrDefault(n => n.Name == sendView.SelectedSignal),
                    value: sendView.Value,
                    broadcast: sendView.Broadcast,
                    devId: sendView.Address
                );

                _ioService.TxQueue.Enqueue(msg);
            };
            #endregion

            #region Tree
            _mainForm.Tree.AfterSelect += Tree_AfterSelect;
            _mainForm.Tree.Nodes.AddRange(
                new TreeNode[]
                    {
                        new View.TreeNodes.RacksTreeNode(_explorer),
                        new View.TreeNodes.ModulsTree(_explorer),
                        new View.TreeNodes.WaitForParseTreeNode(_ioService),
                        new View.TreeNodes.DropFrameTreeNode(_ioService),
                        new View.TreeNodes.ParsedFrameTreeNode(_ioService),
                        new View.TreeNodes.RxFramesTreeNode(_ioService),
                        new View.TreeNodes.TxFramesTreeNode(_ioService),
                        new View.TreeNodes.WaitForTxTreeNode(_ioService),
                        new View.TreeNodes.CanFrameLogTreeNode()
                    });

            _mainForm.Tree.ContextMenuStrip = new ContextMenuStrip();
            _mainForm.Tree.ContextMenuStrip.Items.AddRange(
                new ToolStripItem[]
                {
                    new View.Commands.OpenCanIOLogFileCommand(),
                    new View.Commands.DeleteCanIOLogFileCommand()
                });

            #endregion

            #region DataGrid

            var grid = _mainForm.DataGrid;
            grid.DataSource = _explorer.Devices;
           
            #endregion

            #region StatusBar

            /* StatusBar */
            _mainForm.StatusBar = new ToolStripItem[]
            {
                new StatusBar.WaitForParseFramesStatus(_ioService),
                new StatusBar.ParsedFramesStatus(_ioService),
                new StatusBar.DroppedFramesStatus(_ioService),
                new StatusBar.EmptyStatus(),
                new StatusBar.VersionStatus(),
                new StatusBar.LogoStatus(),
            };

            #endregion

            /* Run */
            Application.Run((MainForm)_mainForm);
        }

        private void Tree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            EventAggregator.Instance.Publish(new TreeNodeChangedAppEvent(e.Node));
        }

        private void IoService_Started(object sender, EventArgs e)
        {
            EventAggregator.Instance.Publish(new PlayAppEvent(_ioService));
            _explorer.StartTimeSamp = DateTime.Now;
            TimerService.Instance.Start();
        }

        private void IoService_Stopped(object sender, EventArgs e)
        {
            EventAggregator.Instance.Publish(new StopAppEvent(_ioService));
            TimerService.Instance.Stop();
        }

        void MainForm_Shown(object sender, EventArgs e)
        {
#if TRACE
            Debug.WriteLine(this.GetType().Namespace + "." + this.GetType().Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name + "()");
#endif

            SyncContext = SynchronizationContext.Current;

            /*Megnyitást követően rá áll az Adapter Nódra a TreeView-ban.*/
            _mainForm.Tree.Nodes[0].ExpandAll();
            _mainForm.Tree.SelectedNode = _startTreeNode;

            //_mainForm.LayoutRestore();
            /*Ö tölti be a projectet*/
            Start(Environment.GetCommandLineArgs());
            /*Kezdő Node Legyen az Adapter node*/
            //EventAggregator.Instance.Publish<TreeViewSelectionChangedAppEvent>(new TreeViewSelectionChangedAppEvent(_startTreeNode));

            EventAggregator.Instance.Publish(new ShowAppEvent());

        }

        public void Start(string[] args)
        {
#if TRACE
            Debug.WriteLine(this.GetType().Namespace + "." + this.GetType().Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name + ": " + string.Join("\r\n -", args));
#endif
        }

        void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
#if TRACE
            Debug.WriteLine(this.GetType().Namespace + "." + this.GetType().Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name + "()");
#endif
        }

        void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
#if TRACE
            Debug.WriteLine(this.GetType().Namespace + "." + this.GetType().Name + "." + System.Reflection.MethodBase.GetCurrentMethod().Name + "()");
#endif
            _ioService.Dispose();
            EventAggregator.Instance.Dispose();
            Settings.Default.Save();

        }

    }
}