using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CP77ToolsGui
{
    public delegate void SxDelegate(string leIn,string filepath);//声明委托
    /// <summary>
    /// PacketList.xaml 的交互逻辑
    /// </summary>
    public partial class PacketList : Window
    {
        public string str{ get; set; }
        public event SxDelegate SxEvent;//声明事件
        
        public PacketList(string strRef)
        {
            str = strRef;
            InitializeComponent();
            Init();
        }
        private void PacketList_OnLoaded(object sender, RoutedEventArgs e)
        {
            PerformUnpack(str);
        }
        public event DelReadStdOutput ReadStdOutput;
        public event DelReadErrOutput ReadErrOutput;

        private void PerformUnpack(string StartFileArg)
        {
            string Arguments = "archive -p \"" + StartFileArg + "\" -l";
            retMainWindow.IsEnabled = false;
            RealAction("CP77Tools/CP77Tools.exe", Arguments);

        }
        private void Init()
        {
            //3.将相应函数注册到委托事件中
            ReadStdOutput += new DelReadStdOutput(ReadStdOutputAction);
            ReadErrOutput += new DelReadErrOutput(ReadErrOutputAction);
        }

        private void RealAction(string StartFileName, string StartFileArg)
        {
            Process CmdProcess = new Process();
            CmdProcess.StartInfo.FileName = StartFileName;      // 命令
            CmdProcess.StartInfo.Arguments = StartFileArg;      // 参数
            CmdProcess.StartInfo.CreateNoWindow = true;         // 不创建新窗口
            CmdProcess.StartInfo.UseShellExecute = false;
            CmdProcess.StartInfo.RedirectStandardInput = true;  // 重定向输入
            CmdProcess.StartInfo.RedirectStandardOutput = true; // 重定向标准输出
            CmdProcess.StartInfo.RedirectStandardError = true;  // 重定向错误输出
            CmdProcess.OutputDataReceived += new DataReceivedEventHandler(p_OutputDataReceived);
            CmdProcess.ErrorDataReceived += new DataReceivedEventHandler(p_ErrorDataReceived);
            CmdProcess.EnableRaisingEvents = true;                      // 启用Exited事件
            CmdProcess.Exited += new EventHandler(CmdProcess_Exited);   // 注册进程结束事件

            CmdProcess.Start();
            CmdProcess.BeginOutputReadLine();
            CmdProcess.BeginErrorReadLine();  
        }

        private void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                // 4. 异步调用，需要invoke
                Dispatcher.Invoke(ReadStdOutput, new object[] { e.Data });
            }
        }

        private void p_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Dispatcher.Invoke(ReadErrOutput, new object[] { e.Data });
            }
        }

        private IList<Model.TreeModel> treeList = new List<Model.TreeModel>();

        private void ReadStdOutputAction(string result)
        {
            if (result.Length > 8 && result.Substring(0,8) == "[Normal]")
            {
                string[] FileArray = result.Remove(0,30).Split('\\');

                if (FileArray.Length > 0)
                {
                    Model.TreeModel tree = new Model.TreeModel();
                    tree.Id = result.Remove(0, 30);
                    tree.Name = result.Remove(0, 30);
                    tree.IsExpanded = true;
                    treeList.Add(tree);
                    /*
                    for (int i = 0; i < FileArray.Length; i++)
                    {
                        if (i == 0)
                        {
                            var tempTree = treeList.TakeWhile((num, index) => num.Id == FileArray[i]);
                            if (tempTree.Count() == 0)
                            {
                                Model.TreeModel tree = new Model.TreeModel();
                                tree.Id = FileArray[i];
                                tree.Name = FileArray[i];
                                tree.IsExpanded = true;
                                treeList.Add(tree);
                            }
                        }
                        else
                        {
                            //根目录
                            var ParentTree = treeList.TakeWhile((num, index) => num.Id == FileArray[0]);

                            //遍历除了根外的目录或文件
                            for (int l = 1; l <= i; l++)
                            {
                                Console.WriteLine(FileArray[l]);
                                Console.WriteLine(ParentTree);
                                Console.WriteLine("---------------------------------");
                                if (ParentTree.Count() != 0)
                                    Console.WriteLine("ParentTree:   " + ParentTree.FirstOrDefault().Id);
                                else
                                    Console.WriteLine("ParentTree: null");

                                //检查父目录是否存在
                                if (ParentTree.Count() != 0)
                                {
                                    //获取子集中的下一个目录
                                    var tempParentTree = ParentTree.FirstOrDefault().Children.TakeWhile((num, index) => num.Id == FileArray[l]);
                                    if(tempParentTree.Count() != 0)
                                    {
                                        ParentTree = tempParentTree;
                                    }
                                    else
                                    {
                                        if (ParentTree.Count() != 0)
                                            Console.WriteLine("AddParentTree:   " + FileArray[l] +"  to  " + ParentTree.FirstOrDefault().Id);

                                        Model.TreeModel child = new Model.TreeModel();
                                        child.Id = FileArray[l];
                                        child.Name = FileArray[l];
                                        child.Parent = ParentTree.FirstOrDefault();
                                        //向父目录添加子集
                                        ParentTree.FirstOrDefault().Children.Add(child);

                                        //获取刚添加的子集
                                        tempParentTree = ParentTree.FirstOrDefault().Children.TakeWhile((num, index) => num.Id == FileArray[l]);
                                        if (tempParentTree.Count() != 0)
                                            ParentTree = tempParentTree;
                                    }
                                }
                            }
                        }
                    }
                    */
                }

            }
        }

        private void ReadErrOutputAction(string result)
        {
            Console.WriteLine(result);
        }

        private void CmdProcess_Exited(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new Action(delegate
            {
                FileListView.ItemsSourceData = treeList;
                retMainWindow.IsEnabled = true;
                loading.Visibility = Visibility.Hidden;
            }));
        }

        /// <summary>
        /// 数据绑定
        /// </summary>
        private void TvTestDataBind()
        {
            IList<Model.TreeModel> treeList = new List<Model.TreeModel>();
            for (int i = 0; i < 5; i++)
            {
                Model.TreeModel tree = new Model.TreeModel();
                tree.Id = i.ToString();
                tree.Name = "Test" + i;
                tree.IsExpanded = true;
                for (int j = 0; j < 5; j++)
                {
                    Model.TreeModel child = new Model.TreeModel();
                    child.Id = i + "-" + j;
                    child.Name = "Test" + child.Id;
                    child.Parent = tree;
                    tree.Children.Add(child);
                }
                treeList.Add(tree);
            }

            FileListView.ItemsSourceData = treeList;

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            IList<Model.TreeModel> treeList = FileListView.CheckedItemsIgnoreRelation();

            string rex = "-r (";

            foreach (Model.TreeModel tree in treeList)
            {

                rex += tree.Id.Replace("\\", "\\\\").Replace(".", "\\.") + "|";
            }
            rex = rex.Remove(rex.Length - 2);
            rex += ")";

            SxEvent?.Invoke(rex, str);
            Close();
            Console.WriteLine(rex);

        }
    }
}
