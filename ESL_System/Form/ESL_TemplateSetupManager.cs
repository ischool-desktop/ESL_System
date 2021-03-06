﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FISCA.Presentation.Controls;
using FISCA.Data;
using DevComponents.DotNetBar;
using System.Xml.Linq;
using K12.Data;
using System.Xml;


namespace ESL_System.Form
{
    public partial class ESL_TemplateSetupManager : FISCA.Presentation.Controls.BaseForm
    {
        private ButtonItem _current_item;

        private BackgroundWorker _workder;

        private Dictionary<string, string> _hintGuideDict = new Dictionary<string, string>();

        private Dictionary<string, string> _typeCovertDict = new Dictionary<string, string>();
        private Dictionary<string, string> _typeCovertRevDict = new Dictionary<string, string>();

        private Dictionary<string, string> _teacherRoleCovertDict = new Dictionary<string, string>();
        private Dictionary<string, string> _teacherRoleCovertRevDict = new Dictionary<string, string>();

        private Dictionary<string, string> _nodeTagCovertDict = new Dictionary<string, string>();

        private Dictionary<string, string> _oriTemplateDescriptionDict = new Dictionary<string, string>();

        private Dictionary<string, string> _examID_NameDict = new Dictionary<string, string>(); // 系統 <examID,exam_name>
        private Dictionary<string, string> _examName_IDDict = new Dictionary<string, string>(); // 系統 <exam_name,examID>

        private ButtonItem currentItem
        {
            get { return _current_item; }
            set { _current_item = value; }
        }

        // 現在點在哪一小節
        DevComponents.AdvTree.Node node_now;

        public ESL_TemplateSetupManager()
        {
            InitializeComponent();
            HideNavigationBar();// 將左下角功能藏起來

            // 對照字典設定
            #region 對照字典設定
            _hintGuideDict.Add("string", "請輸入文字，不得空白、重覆。");
            _hintGuideDict.Add("integer", "請輸入整數數字");
            _hintGuideDict.Add("time", "請輸入日期(ex : 2018/04/21 00:00:00)");
            _hintGuideDict.Add("teacherKind", "點選左鍵選取: 教師一、教師二、教師三");
            _hintGuideDict.Add("ScoreKind", "點選左鍵選取:分數、指標、評語");
            _hintGuideDict.Add("AllowCustom", "點選左鍵選取:是、否");
            _hintGuideDict.Add("Exam", "選擇對應系統評量名稱，不得空白、重覆。");

            _typeCovertDict.Add("Score", "分數");
            _typeCovertDict.Add("Indicator", "指標");
            _typeCovertDict.Add("Comment", "評語");

            _typeCovertRevDict.Add("分數", "Score");
            _typeCovertRevDict.Add("指標", "Indicator");
            _typeCovertRevDict.Add("評語", "Comment");

            _teacherRoleCovertDict.Add("1", "教師一");
            _teacherRoleCovertDict.Add("2", "教師二");
            _teacherRoleCovertDict.Add("3", "教師三");

            _teacherRoleCovertRevDict.Add("教師一", "1");
            _teacherRoleCovertRevDict.Add("教師二", "2");
            _teacherRoleCovertRevDict.Add("教師三", "3");

            _nodeTagCovertDict.Add("term", "試別");
            _nodeTagCovertDict.Add("subject", "科目");
            _nodeTagCovertDict.Add("assessment", "評量");
            _nodeTagCovertDict.Add("string", "指標");
            #endregion

            //載入資料
            LoadAssessmentSetups();
        }

        /// <summary>
        /// 非同步處理，使用時要小心。
        /// </summary>
        private void LoadAssessmentSetups()
        {
            _workder = new BackgroundWorker();
            _workder.DoWork += new DoWorkEventHandler(Workder_DoWork);
            _workder.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workder_RunWorkerCompleted);

            _workder.RunWorkerAsync();

        }

        private void Workder_DoWork(object sender, DoWorkEventArgs e)
        {
            // 2018/05/01 穎華重要備註， 在table exam_template 欄位 description 不為空代表其為ESL 的樣板
            string query = "select * from exam_template where description !=''";

            QueryHelper qh = new QueryHelper();
            DataTable dt = qh.Select(query);

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {

                    ButtonItem item = new ButtonItem();
                    item.Text = "" + dr[1];
                    item.Tag = "" + dr[0];
                    item.OptionGroup = "AssessmentSetup";
                    item.Click += new EventHandler(AssessmentSetup_Click); //點一下 將焦點轉換
                    item.DoubleClick += new EventHandler(AssessmentSetup_DoubleClick); //連點兩下 進入更名視窗
                    ipList.Items.Add(item);

                    // 紀錄原本樣板 Description 資料，作為比較用
                    if (!_oriTemplateDescriptionDict.ContainsKey("" + dr[0]))
                    {
                        _oriTemplateDescriptionDict.Add("" + dr[0], "" + dr["description"]);
                    }
                }
            }


            query = "select * from exam";

            qh = new QueryHelper();
            dt = qh.Select(query);

            if (dt.Rows.Count > 0)
            {
                _examID_NameDict.Clear();
                _examName_IDDict.Clear();

                foreach (DataRow dr in dt.Rows)
                {
                    _examID_NameDict.Add("" + dr["id"], "" + dr["exam_name"]);
                    _examName_IDDict.Add("" + dr["exam_name"], "" + dr["id"]);
                }
            }

        }

        private void Workder_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                {
                    //CurrentUser.ReportError(e.Error);
                    DisableFunctions();
                    AfterLoadAssessmentSetup();
                    MsgBox.Show("下載評量設定資料錯誤。", Application.ProductName);
                    return;
                }

                AfterLoadAssessmentSetup();
            }
            catch (Exception ex)
            {
                MsgBox.Show(ex.Message);
            }
        }

        private void BeforeLoadAssessmentSetup()
        {
            //將設計範例全部清光，開始抓取table 資料
            advTree1.Nodes.Clear();

            currentItem = null;
            _oriTemplateDescriptionDict.Clear(); //將OriData 紀錄全部清光

            Loading = true;
            ipList.Items.Clear();

            advTree1.Enabled = false;
            btnSave.Enabled = false;
        }

        private void SelectAssessmentSetup(ButtonItem item)
        {
            advTree1.Nodes.Clear();

            //沒東西 預設空畫面
            if (item == null)
            {
                return;
            }
            else
            {
                string esl_exam_template_id = "" + item.Tag;

                // 取得ESL 描述 in description
                DataTable dt;
                QueryHelper qh = new QueryHelper();

                string selQuery = "select id,description from exam_template where id = '" + esl_exam_template_id + "'";
                dt = qh.Select(selQuery);
                string xmlStr = "<root>" + dt.Rows[0]["description"].ToString() + "</root>";
                XElement elmRoot = XElement.Parse(xmlStr);

                //解析讀下來的 descriptiony 資料，打包成物件群 最後交給 ParseDBxmlToNodeUI() 處理
                if (elmRoot != null)
                {
                    if (elmRoot.Element("ESLTemplate") != null)
                    {
                        foreach (XElement ele_term in elmRoot.Element("ESLTemplate").Elements("Term"))
                        {
                            Term t = new Term();

                            t.Name = ele_term.Attribute("Name").Value;
                            t.Weight = ele_term.Attribute("Weight").Value;
                            t.InputStartTime = ele_term.Attribute("InputStartTime").Value;
                            t.InputEndTime = ele_term.Attribute("InputEndTime").Value;

                            t.Ref_exam_id = ele_term.Attribute("Ref_exam_id") != null ? ele_term.Attribute("Ref_exam_id").Value : ""; // 2018/09/26 穎驊新增，因應要將 ESL 評量導入 成績系統，恩正說，在此加入評量對照

                            t.SubjectList = new List<Subject>();

                            foreach (XElement ele_subject in ele_term.Elements("Subject"))
                            {
                                Subject s = new Subject();

                                s.Name = ele_subject.Attribute("Name").Value;
                                s.Weight = ele_subject.Attribute("Weight").Value;

                                s.AssessmentList = new List<Assessment>();

                                foreach (XElement ele_assessment in ele_subject.Elements("Assessment"))
                                {
                                    Assessment a = new Assessment();

                                    a.Name = ele_assessment.Attribute("Name").Value;
                                    a.Weight = ele_assessment.Attribute("Weight").Value;
                                    a.TeacherSequence = ele_assessment.Attribute("TeacherSequence").Value;
                                    a.Type = ele_assessment.Attribute("Type").Value;
                                    a.AllowCustomAssessment = ele_assessment.Attribute("AllowCustomAssessment").Value;

                                    if (a.Type == "Comment") // 假如是 評語類別，多讀一項 輸入限制屬性
                                    {
                                        a.InputLimit = ele_assessment.Attribute("InputLimit").Value;
                                    }

                                    a.IndicatorsList = new List<Indicators>();

                                    if (ele_assessment.Element("Indicators") != null)
                                    {

                                        foreach (XElement ele_Indicator in ele_assessment.Element("Indicators").Elements("Indicator"))
                                        {
                                            Indicators i = new Indicators();

                                            i.Name = ele_Indicator.Attribute("Name").Value;
                                            i.Description = ele_Indicator.Attribute("Description").Value;

                                            a.IndicatorsList.Add(i);
                                        }
                                    }
                                    s.AssessmentList.Add(a);
                                }
                                t.SubjectList.Add(s);
                            }
                            ParseDBxmlToNodeUI(t);
                        }

                        CalculatePercentageToUI(true); //第一次載畫面 =true
                    }
                }
            }
        }

        private void AssessmentSetup_Click(object sender, EventArgs e)
        {
            if (currentItem == sender) return;

            if (!CanContinue()) return;

            currentItem = sender as ButtonItem;
            SelectAssessmentSetup(currentItem);

            //在選取ESL評分樣版後， 將報表樣版設定開關啟動
            linkLabel1.Enabled = true;
            linkLabel2.Enabled = true;
            linkLabel3.Enabled = true;
            btnSave.Enabled = true;
        }

        private bool CanContinue()
        {
            if (lblIsDirty.Visible)
            {
                DialogResult dr = MsgBox.Show("您未儲存目前資料，是否要儲存？", Application.ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (dr == DialogResult.Cancel)
                {
                    currentItem.RaiseClick();
                    return false;
                }
                else if (dr == DialogResult.Yes)
                {
                    btnSave_Click(null, null); //儲存
                }
            }
            return true;
        }

        // 點ESL 樣板兩下可以進入 更名視窗
        private void AssessmentSetup_DoubleClick(object sender, EventArgs e)
        {
            if (!CanContinue()) return;

            TemplateReNameForm editor = new TemplateReNameForm(currentItem);
            DialogResult dr = editor.ShowDialog();

            //更名過後，畫面renew
            if (dr == DialogResult.OK)
            {
                try
                {
                    BeforeLoadAssessmentSetup();
                    LoadAssessmentSetups();
                }
                catch (Exception ex)
                {
                    MsgBox.Show(ex.Message);
                }
            }
        }

        private void HideNavigationBar()
        {
            npLeft.NavigationBar.Visible = false;
        }

        private void AfterLoadAssessmentSetup()
        {
            ipList.RecalcLayout();

            lblIsDirty.Visible = false;
            advTree1.Enabled = true;


            Loading = false;
        }

        private void DisableFunctions()
        {
            npLeft.Enabled = false;
        }

        private bool Loading
        {
            get { return loading.Visible; }
            set { loading.Visible = value; }
        }

        private void ESL_TemplateSetupManager_Load(object sender, EventArgs e)
        {
            BeforeLoadAssessmentSetup();
        }

        // 新增、刪除 Node 按鈕
        private void NodeMouseDown_InsertDelete(object sender, MouseEventArgs e)
        {
            node_now = (DevComponents.AdvTree.Node)sender;

            if (node_now.SelectedCell == null || node_now.Cells[0] != node_now.SelectedCell)
            {
                advTree1.ContextMenu = null;
                return;
            }

            MenuItem[] menuItems = new MenuItem[0];

            MenuItem menuItems_insert = new MenuItem("新增" + _nodeTagCovertDict[node_now.TagString], MenuItemInsert_Click);

            MenuItem menuItems_delete = new MenuItem("刪除" + _nodeTagCovertDict[node_now.TagString], MenuItemDelete_Click);

            // 如果只剩下一項，將刪除鍵 disable 不讓使用者 刪除
            // 試別為最上層，其parent 為null ， 需要至advtree1 檢查
            if (node_now.Parent != null)
            {
                if (node_now.TagString == "string" && node_now.Parent.Nodes.Count == 1) // 指標 項目本來就沒有項目
                {
                    menuItems_delete.Enabled = false;
                }
                if (node_now.TagString == "assessment" && node_now.Parent.Nodes.Count - 2 == 1) // 評量上 項目本來有固定項目 2個
                {
                    menuItems_delete.Enabled = false;
                }
                if (node_now.TagString == "subject" && node_now.Parent.Nodes.Count - 4 == 1) // 科目上 項目本來有固定項目 2個
                {
                    menuItems_delete.Enabled = false;
                }
            }
            else
            {
                if (advTree1.Nodes.Count == 1)
                {
                    menuItems_delete.Enabled = false;
                }
            }

            //menuItems = new MenuItem[] { new MenuItem("新增" + nodeTagCovertDict[node_now.TagString], MenuItemInsert_Click), new MenuItem("刪除" + nodeTagCovertDict[node_now.TagString], MenuItemDelete_Click) };

            menuItems = new MenuItem[] { menuItems_insert, menuItems_delete };

            if (e.Button == MouseButtons.Right)
            {
                ContextMenu buttonMenu = new ContextMenu(menuItems);

                advTree1.ContextMenu = buttonMenu;

                advTree1.ContextMenu.Show(advTree1, e.Location);
            }
        }

        //新增 node 子節項目
        private void MenuItemInsert_Click(Object sender, System.EventArgs e)
        {
            // 試別為最上層，其parent 為null ， 需要至advtree1 新增
            if (node_now.Parent != null)
            {
                switch (node_now.Tag)
                {
                    //設定指標 (2018/4/27 穎驊筆記。 目前其Tag 為 string 可能還要再調整)
                    case "string":

                        InsertIndicatorSettingNode(); // IndicatorSettingNode 其設定 稍微異於AssessmentNode、SubjectNode、TermNode，故另外寫功能
                        break;

                    case "assessment":
                        DevComponents.AdvTree.Node a = BuildAssessmentNode(); // 組裝AssessmentNode
                        a.Expanded = true; //將項目展開
                        node_now.Parent.Nodes.Add(a);
                        break;
                    case "subject":

                        DevComponents.AdvTree.Node s_a = BuildAssessmentNode();// 組裝AssessmentNode
                        DevComponents.AdvTree.Node s = BuildSubjectNode(); // 組裝SubjectNode

                        s.Nodes.Add(s_a); //將組裝好的 AssessmentNode加入 SubjectNode
                        s.Expanded = true; //將項目展開
                        node_now.Parent.Nodes.Add(s);

                        break;
                    default:
                        break;
                }

                CalculatePercentageToUI(false);// 加上百分比資料 非第一次載入 =false
            }
            else
            {
                //term 會在此新增

                DevComponents.AdvTree.Node t_s_a = BuildAssessmentNode();// 組裝AssessmentNode
                DevComponents.AdvTree.Node s_a = BuildSubjectNode(); // 組裝SubjectNode

                s_a.Nodes.Add(t_s_a); //將組裝好的 AssessmentNode加入 SubjectNode
                s_a.Expanded = true; //將項目展開

                DevComponents.AdvTree.Node t = BuildTermNode(); // 組裝TermNode

                t.Nodes.Add(s_a); //將組裝好的 SubjectNode加入 TermNode
                t.Expanded = true; //將項目展開
                advTree1.Nodes.Add(t); //將組裝好的 SubjectNode加入 advTree(最上層)
                CalculatePercentageToUI(false);// 加上百分比資料 非第一次載入 =false
            }

            IsDirtyOrNot();// 檢查是否有更動資料
        }

        //刪除 node 子節項目
        private void MenuItemDelete_Click(Object sender, System.EventArgs e)
        {
            // 試別為最上層，其parent 為null ， 需要至advtree1 新增
            if (node_now.Parent != null)
            {
                node_now.Parent.Nodes.Remove(node_now);
                CalculatePercentageToUI(false);// 加上百分比資料 非第一次載入 =false
            }
            else
            {
                advTree1.Nodes.Remove(node_now);
                CalculatePercentageToUI(false);// 加上百分比資料 非第一次載入 =false
            }

            IsDirtyOrNot();// 檢查是否有更動資料
        }

        // 點下評量項目後，提供選項讓使用者選擇
        private void NodeMouseDown(object sender, MouseEventArgs e)
        {
            node_now = (DevComponents.AdvTree.Node)sender;

            if (node_now.SelectedCell == null || node_now.Cells[1] != node_now.SelectedCell)
            {
                advTree1.ContextMenu = null;
                return;
            }

            MenuItem[] menuItems = new MenuItem[0];

            switch (node_now.Tag)
            {
                case "teacherKind":
                    //Declare the menu items and the shortcut menu.
                    menuItems = new MenuItem[]{new MenuItem("教師一",MenuItemNew_Click),
                new MenuItem("教師二",MenuItemNew_Click), new MenuItem("教師三",MenuItemNew_Click)};
                    LeftMouseClick(menuItems, e);
                    break;

                case "ScoreKind":
                    //Declare the menu items and the shortcut menu.
                    menuItems = new MenuItem[]{new MenuItem("分數",MenuItemNew_Click),
                new MenuItem("指標",MenuItemNew_Click), new MenuItem("評語",MenuItemNew_Click)};
                    LeftMouseClick(menuItems, e);
                    break;
                case "AllowCustom":
                    //Declare the menu items and the shortcut menu.
                    menuItems = new MenuItem[]{new MenuItem("是",MenuItemNew_Click),
                new MenuItem("否",MenuItemNew_Click)};
                    LeftMouseClick(menuItems, e);
                    break;
                case "Exam":
                    //Declare the menu items and the shortcut menu.

                    List<MenuItem> mList = new List<MenuItem>();

                    foreach (string exam in _examName_IDDict.Keys)
                    {
                        MenuItem m = new MenuItem(exam, MenuItemNew_Click);

                        mList.Add(m);
                    }

                    menuItems = mList.ToArray();

                    LeftMouseClick(menuItems, e);
                    break;
                default:
                    break;
            }

            //menuItem 的另一種寫法
            //ContextMenuStrip contexMenuuu = new ContextMenuStrip();
            //contexMenuuu.Items.Add("Edit ");
            //contexMenuuu.Items.Add("Delete ");
            //contexMenuuu.Show();
            //contexMenuuu.ItemClicked += new ToolStripItemClickedEventHandler(
            //    contexMenuuu_ItemClicked);

        }

        //  將右鍵點選的項目(ex: 教師一、教師二、教師三) 指定給 目前所選node 的第二個cell 
        private void MenuItemNew_Click(Object sender, System.EventArgs e)
        {
            System.Windows.Forms.MenuItem mi = (System.Windows.Forms.MenuItem)sender;

            node_now.Cells[1].Text = mi.Text;

            // 選擇老師後 更新項目名稱 評量(XXX,教師一)
            if (node_now.TagString == "teacherKind")
            {
                node_now.Parent.Cells[0].Text = _nodeTagCovertDict["" + node_now.Parent.Tag] + "(" + node_now.Parent.Nodes[0].Cells[1].Text + "," + node_now.SelectedCell.Text + ",)";

                CalculatePercentageToUI(false);// 加上百分比資料 非第一次載入 =false
            }

            // 選擇試別後  檢驗規則
            if (node_now.TagString == "Exam")
            {
                DevComponents.AdvTree.CellEditEventArgs _e = new DevComponents.AdvTree.CellEditEventArgs(node_now.Cells[2], DevComponents.AdvTree.eTreeAction.Mouse, "");

                advTree1_AfterCellEditComplete(advTree1, _e);
            }


            // 假若分數指標沒有選項，則會只再 新增"一項" 指標子項目設定
            if (mi.Text == "指標")
            {
                // 選擇指標後，將比重設定為0，且disable
                node_now.Parent.Nodes[1].Cells[1].Text = "0";
                node_now.Parent.Nodes[1].Cells[2].Text = "指標型評量無法輸入比例";
                node_now.Parent.Nodes[1].Enabled = false;

                //假如甚麼都沒有，就先加入起始
                if (node_now.Nodes.Count == 0)
                {
                    DevComponents.AdvTree.Node new_indicator_setting_node = new DevComponents.AdvTree.Node();

                    new_indicator_setting_node.Tag = "string";
                    new_indicator_setting_node.Text = "指標(請輸入名稱)";

                    DevComponents.AdvTree.Node new_indicators_node_name = new DevComponents.AdvTree.Node(); //指標名稱
                    DevComponents.AdvTree.Node new_indicators_node_description = new DevComponents.AdvTree.Node(); //指標描述

                    //項目
                    new_indicators_node_name.Text = "名稱:";
                    new_indicators_node_description.Text = "描述:";

                    //node Tag            
                    new_indicators_node_name.Tag = "string";

                    //值
                    new_indicators_node_name.Cells.Add(new DevComponents.AdvTree.Cell());
                    new_indicators_node_description.Cells.Add(new DevComponents.AdvTree.Cell());


                    //說明
                    new_indicators_node_name.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_indicators_node_name.Tag]));
                    new_indicators_node_description.Cells.Add(new DevComponents.AdvTree.Cell("請填入此指標項目的說明，以利評分老師了解。"));


                    ////設定為不能點選編輯，避免使用者誤用
                    new_indicators_node_name.Cells[0].Editable = false;
                    new_indicators_node_name.Cells[2].Editable = false;
                    new_indicators_node_description.Cells[0].Editable = false;


                    //設定為不能拖曳，避免使用者誤用
                    new_indicators_node_name.DragDropEnabled = false;
                    new_indicators_node_description.DragDropEnabled = false;

                    new_indicator_setting_node.Nodes.Add(new_indicators_node_name);
                    new_indicator_setting_node.Nodes.Add(new_indicators_node_description);

                    //不可編輯不可、拖曳
                    new_indicator_setting_node.Editable = false;
                    new_indicator_setting_node.DragDropEnabled = false;

                    //加入新增刪除按鈕
                    new_indicator_setting_node.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown_InsertDelete);

                    //展開
                    new_indicator_setting_node.Expanded = true;

                    node_now.Nodes.Add(new_indicator_setting_node);

                    //展開
                    node_now.Expanded = true;
                }

                CalculatePercentageToUI(false);// 加上百分比資料 非第一次載入 =false
            }
            else
            {
                // 選非指標，將其還原               
                if (node_now.TagString == "ScoreKind")
                {
                    node_now.Parent.Nodes[1].Enabled = true;
                    node_now.Parent.Nodes[1].Cells[2].Text = _hintGuideDict[node_now.Parent.Nodes[1].TagString];

                    if (mi.Text == "評語")
                    {
                        node_now.Parent.Nodes[1].Cells[1].Text = "0"; // 切換到評語 則為0
                        DevComponents.AdvTree.Node new_assessment_node_inputLimit = new DevComponents.AdvTree.Node(); //輸入限制(專給Comment 使用)
                        new_assessment_node_inputLimit.Text = "輸入限制";
                        new_assessment_node_inputLimit.Tag = "integer";
                        new_assessment_node_inputLimit.Cells.Add(new DevComponents.AdvTree.Cell("200")); // 預設限制200 字
                        new_assessment_node_inputLimit.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_inputLimit.Tag]));
                        new_assessment_node_inputLimit.Cells[0].Editable = false;
                        new_assessment_node_inputLimit.Cells[2].Editable = false;
                        new_assessment_node_inputLimit.DragDropEnabled = false;

                        node_now.Parent.Nodes.Add(new_assessment_node_inputLimit);
                    }
                    if (mi.Text == "分數" && node_now.Parent.Nodes.Count > 5)
                    {
                        node_now.Parent.Nodes.RemoveAt(node_now.Parent.Nodes.Count - 1); // 假若從 評語選回 分數 把最後一項 輸入限制刪掉。

                    }


                    node_now.Nodes.Clear();
                }

                CalculatePercentageToUI(false);// 加上百分比資料 非第一次載入 =false
            }

            IsDirtyOrNot();// 檢查是否有更動資料
        }

        // 在編輯完之後驗證使用者輸入的資料是否符合型態
        private void advTree1_AfterCellEditComplete(object sender, DevComponents.AdvTree.CellEditEventArgs e)
        {
            DevComponents.AdvTree.AdvTree advt = (DevComponents.AdvTree.AdvTree)sender;

            node_now = advt.SelectedNode;

            switch (node_now.Tag)
            {
                // 整數
                case "integer":
                    if (!int.TryParse(node_now.SelectedCell.Text, out int check_cell_int))
                    {
                        //node_now.Style = DevComponents.AdvTree.NodeStyles.Red;
                        //node_now.StyleSelected = DevComponents.AdvTree.NodeStyles.Red;
                        node_now.Cells[2].Text = "<b><font color=\"#ED1C24\">" + _hintGuideDict["" + node_now.Tag] + "</font></b>"; //輸入規則錯誤，顯示紅字
                        btnSave.Enabled = false;
                    }
                    else
                    {
                        //node_now.Style = null;
                        //node_now.StyleSelected = null;
                        node_now.Cells[2].Text = _hintGuideDict["" + node_now.Tag]; //輸入規則正確
                        btnSave.Enabled = true;

                        CalculatePercentageToUI(false);// 加上百分比資料 非第一次載入 =false
                    }
                    break;
                // 時間
                case "time":
                    if (!DateTime.TryParse(node_now.SelectedCell.Text, out DateTime check_cell_DateTime))
                    {
                        //node_now.Style = DevComponents.AdvTree.NodeStyles.Red;
                        //node_now.StyleSelected = DevComponents.AdvTree.NodeStyles.Red;
                        node_now.Cells[2].Text = "<b><font color=\"#ED1C24\">" + _hintGuideDict["" + node_now.Tag] + "</font></b>"; //輸入規則錯誤，顯示紅字
                        btnSave.Enabled = false;
                    }
                    else
                    {
                        //node_now.Style = null;
                        //node_now.StyleSelected = null;
                        node_now.Cells[2].Text = _hintGuideDict["" + node_now.Tag]; //輸入規則正確
                        btnSave.Enabled = true;
                    }
                    break;
                // 字串，名稱必定要輸入，且不能重覆
                case "string":

                    if (node_now.SelectedCell.Text == "" || CheckDuplicated())
                    {
                        //node_now.Style = DevComponents.AdvTree.NodeStyles.Red;
                        //node_now.StyleSelected = DevComponents.AdvTree.NodeStyles.Red;
                        node_now.Cells[2].Text = "<b><font color=\"#ED1C24\">" + _hintGuideDict["" + node_now.Tag] + "</font></b>"; //輸入規則錯誤，顯示紅字

                        btnSave.Enabled = false;
                    }
                    else
                    {
                        //node_now.Style = null;
                        //node_now.StyleSelected = null;
                        node_now.Cells[2].Text = _hintGuideDict["" + node_now.Tag]; //輸入規則正確

                        // 更新項目名稱
                        if (node_now.Cells[0].Text == "名稱:")
                        {
                            if (node_now.Parent.TagString != "assessment" && node_now.Parent.TagString != null)
                            {
                                node_now.Parent.Cells[0].Text = _nodeTagCovertDict["" + node_now.Parent.Tag] + "(" + node_now.SelectedCell.Text + ",)";
                            }
                            else
                            {
                                // 加上評分 教師腳色
                                node_now.Parent.Cells[0].Text = _nodeTagCovertDict["" + node_now.Parent.Tag] + "(" + node_now.SelectedCell.Text + "," + node_now.Parent.Nodes[2].Cells[1].Text + ",)";
                            }
                        }
                        btnSave.Enabled = true;
                    }
                    break;
                // 試別
                case "Exam":
                    if (node_now.SelectedCell.Text == "" || CheckDuplicated())
                    {
                        //node_now.Style = DevComponents.AdvTree.NodeStyles.Red;
                        //node_now.StyleSelected = DevComponents.AdvTree.NodeStyles.Red;
                        node_now.Cells[2].Text = "<b><font color=\"#ED1C24\">" + _hintGuideDict["" + node_now.Tag] + "</font></b>"; //輸入規則錯誤，顯示紅字
                        btnSave.Enabled = false;
                    }
                    else
                    {
                        //node_now.Style = null;
                        //node_now.StyleSelected = null;
                        node_now.Cells[2].Text = _hintGuideDict["" + node_now.Tag]; //輸入規則正確
                        btnSave.Enabled = true;
                    }
                    break;
                default:
                    break;
            }

            IsDirtyOrNot();// 檢查是否有更動資料

        }




        private void LeftMouseClick(MenuItem[] menuItems, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ContextMenu buttonMenu = new ContextMenu(menuItems);

                advTree1.ContextMenu = buttonMenu;

                advTree1.ContextMenu.Show(advTree1, e.Location);
            }

        }

        //將資料印至畫面上 (從 Term  > Subject > Assessment > Indicator)
        private void ParseDBxmlToNodeUI(Term t)
        {
            // term node
            DevComponents.AdvTree.Node new_term_node = new DevComponents.AdvTree.Node();
            // 試別(名稱)
            new_term_node.Text = "試別(" + t.Name + ")";
            // Tag
            new_term_node.TagString = "term";

            //加入新增刪除按鈕
            new_term_node.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown_InsertDelete);

            //設定為不能點選編輯，避免使用者誤用
            new_term_node.Cells[0].Editable = false;

            //設定為不能拖曳，避免使用者誤用
            new_term_node.DragDropEnabled = false;

            DevComponents.AdvTree.Node new_term_node_name = new DevComponents.AdvTree.Node(); //試別名稱
            DevComponents.AdvTree.Node new_term_node_percentage = new DevComponents.AdvTree.Node(); //比例
            DevComponents.AdvTree.Node new_term_node_inputStartTime = new DevComponents.AdvTree.Node(); //輸入開始時間
            DevComponents.AdvTree.Node new_term_node_inputEndTime = new DevComponents.AdvTree.Node(); //輸入結束時間
            DevComponents.AdvTree.Node new_term_node_refExamID = new DevComponents.AdvTree.Node(); //對應系統試別名稱

            //項目
            new_term_node_name.Text = "名稱:";
            new_term_node_percentage.Text = "比例:";
            new_term_node_inputStartTime.Text = "成績輸入開始時間:";
            new_term_node_inputEndTime.Text = "成績輸入截止時間:";
            new_term_node_refExamID.Text = "對應系統試別:";
            //node Tag
            new_term_node_name.Tag = "string";
            new_term_node_percentage.Tag = "integer";
            new_term_node_inputStartTime.Tag = "time";
            new_term_node_inputEndTime.Tag = "time";
            new_term_node_refExamID.Tag = "Exam";

            //值
            new_term_node_name.Cells.Add(new DevComponents.AdvTree.Cell(t.Name));
            new_term_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell(t.Weight));
            new_term_node_inputStartTime.Cells.Add(new DevComponents.AdvTree.Cell(t.InputStartTime));
            new_term_node_inputEndTime.Cells.Add(new DevComponents.AdvTree.Cell(t.InputEndTime));
            new_term_node_refExamID.Cells.Add(new DevComponents.AdvTree.Cell(_examID_NameDict.ContainsKey(t.Ref_exam_id) ? _examID_NameDict[t.Ref_exam_id] : ""));

            //說明
            new_term_node_name.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_term_node_name.Tag]));
            new_term_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_term_node_percentage.Tag]));
            new_term_node_inputStartTime.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_term_node_inputStartTime.Tag]));
            new_term_node_inputEndTime.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_term_node_inputEndTime.Tag]));


            //試別有值才給存，無值，標紅色
            if (new_term_node_refExamID.Cells[1].Text != "")
            {
                new_term_node_refExamID.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_term_node_refExamID.Tag]));
                btnSave.Enabled = true;
            }
            else
            {
                new_term_node_refExamID.Cells.Add(new DevComponents.AdvTree.Cell("<b><font color=\"#ED1C24\">" + _hintGuideDict["" + new_term_node_refExamID.Tag] + "</font></b>")); //輸入規則錯誤，顯示紅字                
                btnSave.Enabled = false;
            }



            //點擊事件
            new_term_node_refExamID.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown);

            //設定為不能點選編輯，避免使用者誤用
            new_term_node_name.Cells[0].Editable = false;
            new_term_node_name.Cells[2].Editable = false;
            new_term_node_percentage.Cells[0].Editable = false;
            new_term_node_percentage.Cells[2].Editable = false;
            new_term_node_inputStartTime.Cells[0].Editable = false;
            new_term_node_inputStartTime.Cells[2].Editable = false;
            new_term_node_inputEndTime.Cells[0].Editable = false;
            new_term_node_inputEndTime.Cells[2].Editable = false;
            new_term_node_refExamID.Cells[0].Editable = false;
            new_term_node_refExamID.Cells[2].Editable = false;


            //設定為不能拖曳，避免使用者誤用
            new_term_node_name.DragDropEnabled = false;
            new_term_node_percentage.DragDropEnabled = false;
            new_term_node_inputStartTime.DragDropEnabled = false;
            new_term_node_inputEndTime.DragDropEnabled = false;
            new_term_node_refExamID.DragDropEnabled = false;

            //將子node 加入
            new_term_node.Nodes.Add(new_term_node_name);
            new_term_node.Nodes.Add(new_term_node_percentage);
            new_term_node.Nodes.Add(new_term_node_inputStartTime);
            new_term_node.Nodes.Add(new_term_node_inputEndTime);
            new_term_node.Nodes.Add(new_term_node_refExamID);

            // 科目
            foreach (Subject s in t.SubjectList)
            {
                // subject node
                DevComponents.AdvTree.Node new_subjet_node = new DevComponents.AdvTree.Node();

                // 科目(名稱)
                new_subjet_node.Text = "科目(" + s.Name + ")";

                // Tag
                new_subjet_node.TagString = "subject";

                //加入新增刪除按鈕
                new_subjet_node.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown_InsertDelete);

                DevComponents.AdvTree.Node new_subject_node_name = new DevComponents.AdvTree.Node(); //科目名稱
                DevComponents.AdvTree.Node new_subject_node_percentage = new DevComponents.AdvTree.Node(); //比例

                //項目
                new_subject_node_name.Text = "名稱:";
                new_subject_node_percentage.Text = "比例:";
                //node Tag
                new_subject_node_name.Tag = "string";
                new_subject_node_percentage.Tag = "integer";

                //值
                new_subject_node_name.Cells.Add(new DevComponents.AdvTree.Cell(s.Name));
                new_subject_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell(s.Weight));

                //說明
                new_subject_node_name.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_subject_node_name.Tag]));
                new_subject_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_subject_node_percentage.Tag]));

                //設定為不能點選編輯，避免使用者誤用
                new_subject_node_name.Cells[0].Editable = false;
                new_subject_node_name.Cells[2].Editable = false;
                new_subject_node_percentage.Cells[0].Editable = false;
                new_subject_node_percentage.Cells[2].Editable = false;

                //設定為不能拖曳，避免使用者誤用
                new_subject_node_name.DragDropEnabled = false;
                new_subject_node_percentage.DragDropEnabled = false;

                // 將子node 加入
                new_subjet_node.Nodes.Add(new_subject_node_name);
                new_subjet_node.Nodes.Add(new_subject_node_percentage);

                //設定為不能點選編輯，避免使用者誤用
                new_subjet_node.Cells[0].Editable = false;


                foreach (Assessment a in s.AssessmentList)
                {
                    // assessment node
                    DevComponents.AdvTree.Node new_assessment_node = new DevComponents.AdvTree.Node();

                    // 評量(名稱)
                    new_assessment_node.Text = "評量(" + a.Name + "," + _teacherRoleCovertDict[a.TeacherSequence] + ")";

                    //Tag
                    new_assessment_node.TagString = "assessment";

                    //加入新增刪除按鈕
                    new_assessment_node.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown_InsertDelete);


                    DevComponents.AdvTree.Node new_assessment_node_name = new DevComponents.AdvTree.Node(); //評量名稱
                    DevComponents.AdvTree.Node new_assessment_node_percentage = new DevComponents.AdvTree.Node();   //比例
                    DevComponents.AdvTree.Node new_assessment_node_teacherRole = new DevComponents.AdvTree.Node();  //評分老師
                    DevComponents.AdvTree.Node new_assessment_node_type = new DevComponents.AdvTree.Node(); //評分種類
                    DevComponents.AdvTree.Node new_assessment_node_inputLimit = new DevComponents.AdvTree.Node(); //輸入限制(專給Comment 使用)
                    DevComponents.AdvTree.Node new_assessment_node_allowCustomAssessment = new DevComponents.AdvTree.Node(); //是否允許自訂項目

                    //項目
                    new_assessment_node_name.Text = "名稱:";
                    new_assessment_node_percentage.Text = "比例:";
                    new_assessment_node_teacherRole.Text = "評分老師";
                    new_assessment_node_type.Text = "評分種類";
                    new_assessment_node_inputLimit.Text = "輸入限制";
                    new_assessment_node_allowCustomAssessment.Text = "是否允許自訂項目";

                    //node Tag
                    new_assessment_node_name.Tag = "string";
                    new_assessment_node_percentage.Tag = "integer";
                    new_assessment_node_teacherRole.Tag = "teacherKind";
                    new_assessment_node_type.Tag = "ScoreKind";
                    new_assessment_node_inputLimit.Tag = "integer";
                    new_assessment_node_allowCustomAssessment.Tag = "AllowCustom";

                    //值
                    new_assessment_node_name.Cells.Add(new DevComponents.AdvTree.Cell(a.Name));
                    new_assessment_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell(a.Weight));
                    new_assessment_node_teacherRole.Cells.Add(new DevComponents.AdvTree.Cell(_teacherRoleCovertDict[a.TeacherSequence]));
                    new_assessment_node_type.Cells.Add(new DevComponents.AdvTree.Cell(_typeCovertDict[a.Type]));
                    new_assessment_node_inputLimit.Cells.Add(new DevComponents.AdvTree.Cell(a.InputLimit));
                    new_assessment_node_allowCustomAssessment.Cells.Add(new DevComponents.AdvTree.Cell(a.AllowCustomAssessment == "true" ? "是" : "否"));

                    //說明
                    new_assessment_node_name.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_name.Tag]));
                    new_assessment_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_percentage.Tag]));
                    new_assessment_node_teacherRole.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_teacherRole.Tag]));
                    new_assessment_node_type.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_type.Tag]));
                    new_assessment_node_inputLimit.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_inputLimit.Tag]));
                    new_assessment_node_allowCustomAssessment.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_allowCustomAssessment.Tag]));

                    // 點擊事件 (適用於:teacherKind、ScoreKind、AllowCustom)
                    new_assessment_node_teacherRole.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown);
                    new_assessment_node_type.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown);
                    new_assessment_node_allowCustomAssessment.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown);

                    //設定為不能點選編輯，避免使用者誤用
                    new_assessment_node_name.Cells[0].Editable = false;
                    new_assessment_node_name.Cells[2].Editable = false;
                    new_assessment_node_percentage.Cells[0].Editable = false;
                    new_assessment_node_percentage.Cells[2].Editable = false;
                    new_assessment_node_teacherRole.Cells[0].Editable = false;
                    new_assessment_node_teacherRole.Cells[2].Editable = false;
                    new_assessment_node_type.Cells[0].Editable = false;
                    new_assessment_node_type.Cells[2].Editable = false;
                    new_assessment_node_inputLimit.Cells[0].Editable = false;
                    new_assessment_node_inputLimit.Cells[2].Editable = false;
                    new_assessment_node_allowCustomAssessment.Cells[0].Editable = false;
                    new_assessment_node_allowCustomAssessment.Cells[2].Editable = false;

                    //設定為不能拖曳，避免使用者誤用
                    new_assessment_node_name.DragDropEnabled = false;
                    new_assessment_node_percentage.DragDropEnabled = false;
                    new_assessment_node_teacherRole.DragDropEnabled = false;
                    new_assessment_node_type.DragDropEnabled = false;
                    new_assessment_node_inputLimit.DragDropEnabled = false;
                    new_assessment_node_allowCustomAssessment.DragDropEnabled = false;


                    //假如有指標型評量 則加入最後一層指標型評量輸入
                    if (a.Type == "Indicator")
                    {
                        foreach (Indicators i in a.IndicatorsList)
                        {
                            DevComponents.AdvTree.Node new_indicators_node = new DevComponents.AdvTree.Node();

                            //項目
                            new_indicators_node.Text = "指標(" + i.Name + ")";


                            //加入新增刪除按鈕
                            new_indicators_node.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown_InsertDelete);

                            DevComponents.AdvTree.Node new_indicators_node_name = new DevComponents.AdvTree.Node(); //指標名稱

                            DevComponents.AdvTree.Node new_indicators_node_description = new DevComponents.AdvTree.Node(); //指標描述

                            //項目
                            new_indicators_node_name.Text = "名稱:";
                            new_indicators_node_description.Text = "描述:";

                            //node Tag
                            new_indicators_node.Tag = "string";
                            new_indicators_node_name.Tag = "string";

                            //值
                            new_indicators_node_name.Cells.Add(new DevComponents.AdvTree.Cell(i.Name));
                            new_indicators_node_description.Cells.Add(new DevComponents.AdvTree.Cell(i.Description != "" ? i.Description : ""));


                            //說明
                            new_indicators_node_name.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_indicators_node_name.Tag]));
                            new_indicators_node_description.Cells.Add(new DevComponents.AdvTree.Cell("請填入此指標項目的說明，以利評分老師了解。"));


                            ////設定為不能點選編輯，避免使用者誤用
                            new_indicators_node_name.Cells[0].Editable = false;
                            new_indicators_node_name.Cells[2].Editable = false;
                            new_indicators_node_description.Cells[0].Editable = false;


                            //設定為不能拖曳，避免使用者誤用
                            new_indicators_node_name.DragDropEnabled = false;
                            new_indicators_node_description.DragDropEnabled = false;


                            new_indicators_node.Nodes.Add(new_indicators_node_name);
                            new_indicators_node.Nodes.Add(new_indicators_node_description);

                            //設定為不能點選編輯，避免使用者誤用
                            new_indicators_node.Cells[0].Editable = false;

                            //設定為不能拖曳，避免使用者誤用
                            new_indicators_node.DragDropEnabled = false;

                            //將 new_indicators_node 加入  new_assessment_node_type
                            new_assessment_node_type.Nodes.Add(new_indicators_node);
                        }

                        // 假如其為指標型評量 將比例 設定 0,disable
                        new_assessment_node_percentage.Cells[1].Text = "0";
                        new_assessment_node_percentage.Cells[2].Text = "指標型評量無法輸入比例";
                        new_assessment_node_percentage.Enabled = false;

                        //new_assessment_node_type.Expand(); //預設不展開此項
                    }

                    //加入子 node
                    new_assessment_node.Nodes.Add(new_assessment_node_name);
                    new_assessment_node.Nodes.Add(new_assessment_node_percentage);
                    new_assessment_node.Nodes.Add(new_assessment_node_teacherRole);
                    new_assessment_node.Nodes.Add(new_assessment_node_type);
                    new_assessment_node.Nodes.Add(new_assessment_node_allowCustomAssessment);
                    if (a.Type == "Comment")// 假如是 評語類別，才多加入
                    {
                        new_assessment_node.Nodes.Add(new_assessment_node_inputLimit);
                    }

                    //設定為不能點選編輯，避免使用者誤用
                    new_assessment_node.Cells[0].Editable = false;

                    //設定為不能拖曳，避免使用者誤用
                    new_subjet_node.DragDropEnabled = false;

                    // 將 new_assessment_node 加入 new_subjet_node
                    new_subjet_node.Nodes.Add(new_assessment_node);
                    //展開
                    new_subjet_node.Expand();

                }

                // 將 new_subjet_node 加入 new_term_node
                new_term_node.Nodes.Add(new_subjet_node);
                //展開
                new_term_node.Expand();

            }

            //DevComponents.AdvTree.Node add_new_subject_node_btn = new DevComponents.AdvTree.Node();
            //add_new_subject_node_btn.Text = "<b><font color=\"#ED1C24\">+加入新子項目</font></b>";
            //add_new_subject_node_btn.NodeDoubleClick += new System.EventHandler(InsertNewSubject);
            //new_term_node.Nodes.Add(add_new_subject_node_btn);

            //不用Add 改用Insert ，是因為可以指定位子，讓加入新試別功能可以永遠在最後一項。
            advTree1.Nodes.Add(new_term_node);
            //advTree1.Nodes.Insert(advTree1.Nodes.Count - 1, new_term_node);



        }

        //將各自含有比例項目 計算百分比後，加到項目名稱後面 ，
        //2018/5/7 穎驊備註，這個為了要及時顯示個子項目佔的百分比的方法，寫得非。常。爛，僅是能使用的拼裝車
        //但按照目前顯示子項目的邏輯也只能先暫時這樣寫
        //因為若要全面改寫 名稱顯示的配對，幾乎要設一另一整套的機制，這時間投入不划算，日後若有問題，可以再考慮改寫。
        private void CalculatePercentageToUI(bool firstLoad)
        {
            bool isFirstLoad = firstLoad;


            #region 試別
            decimal term_total = 0;

            foreach (DevComponents.AdvTree.Node node_term in advTree1.Nodes)
            {
                foreach (DevComponents.AdvTree.Node node_subject in node_term.Nodes)
                {
                    if (node_subject.Text == "比例:" && decimal.TryParse(node_subject.Cells[1].Text, out decimal i))
                    {
                        term_total += decimal.Parse(node_subject.Cells[1].Text);
                    }
                }
            }

            foreach (DevComponents.AdvTree.Node node_term in advTree1.Nodes)
            {
                foreach (DevComponents.AdvTree.Node node_subject in node_term.Nodes)
                {
                    if (node_subject.Text == "比例:" && term_total != 0 && decimal.TryParse(node_subject.Cells[1].Text, out decimal i))
                    {
                        if (isFirstLoad)
                        {
                            // 去掉尾端括弧之後 加上百分比
                            node_term.Text = node_term.Text.Substring(0, node_term.Text.Length - 1) + "," + Math.Round(decimal.Parse(node_subject.Cells[1].Text) * 100 / term_total, 2) + "%)";
                        }
                        else
                        {
                            // 砍到最後一個逗號, 加上百分比
                            node_term.Text = node_term.Text.Substring(0, node_term.Text.LastIndexOf(",")) + "," + Math.Round(decimal.Parse(node_subject.Cells[1].Text) * 100 / term_total, 2) + "%)";
                        }

                    }
                }
            }
            #endregion

            #region 科目
            //decimal subject_total = 0;

            Dictionary<string, decimal> node_subject_total_Dict = new Dictionary<string, decimal>();

            foreach (DevComponents.AdvTree.Node node_term in advTree1.Nodes)
            {
                foreach (DevComponents.AdvTree.Node node_subject in node_term.Nodes)
                {
                    foreach (DevComponents.AdvTree.Node node_assessment in node_subject.Nodes)
                    {
                        if (node_assessment.Text == "比例:" && decimal.TryParse(node_assessment.Cells[1].Text, out decimal i))
                        {
                            if (!node_subject_total_Dict.ContainsKey(node_term.Text))
                            {
                                node_subject_total_Dict.Add(node_term.Text, 0);

                                node_subject_total_Dict[node_term.Text] += decimal.Parse(node_assessment.Cells[1].Text);
                            }
                            else
                            {
                                node_subject_total_Dict[node_term.Text] += decimal.Parse(node_assessment.Cells[1].Text);
                            }
                        }
                    }
                }
            }

            foreach (DevComponents.AdvTree.Node node_term in advTree1.Nodes)
            {
                foreach (DevComponents.AdvTree.Node node_subject in node_term.Nodes)
                {
                    foreach (DevComponents.AdvTree.Node node_assessment in node_subject.Nodes)
                    {
                        if (node_assessment.Text == "比例:" && node_subject_total_Dict.ContainsKey(node_term.Text) && node_subject_total_Dict[node_term.Text] != 0 && decimal.TryParse(node_assessment.Cells[1].Text, out decimal i))
                        {
                            if (isFirstLoad)
                            {
                                // 去掉尾端括弧之後 加上百分比
                                node_subject.Text = node_subject.Text.Substring(0, node_subject.Text.Length - 1) + "," + Math.Round(decimal.Parse(node_assessment.Cells[1].Text) * 100 / node_subject_total_Dict[node_term.Text], 2) + "%)";
                            }
                            else
                            {
                                // 砍到最後一個逗號, 加上百分比
                                node_subject.Text = node_subject.Text.Substring(0, node_subject.Text.LastIndexOf(",")) + "," + Math.Round(decimal.Parse(node_assessment.Cells[1].Text) * 100 / node_subject_total_Dict[node_term.Text], 2) + "%)";
                            }

                        }
                    }
                }
            }
            #endregion

            #region 評量            
            Dictionary<string, decimal> node_assessment_total_Dict = new Dictionary<string, decimal>();

            foreach (DevComponents.AdvTree.Node node_term in advTree1.Nodes)
            {
                foreach (DevComponents.AdvTree.Node node_subject in node_term.Nodes)
                {
                    foreach (DevComponents.AdvTree.Node node_assessment in node_subject.Nodes)
                    {
                        foreach (DevComponents.AdvTree.Node node_assessment_sub in node_assessment.Nodes)
                        {
                            if (node_assessment_sub.Text == "比例:" && decimal.TryParse(node_assessment_sub.Cells[1].Text, out decimal i))
                            {
                                if (!node_assessment_total_Dict.ContainsKey(node_term.Text + "_" + node_subject.Text))
                                {
                                    node_assessment_total_Dict.Add(node_term.Text + "_" + node_subject.Text, 0);

                                    node_assessment_total_Dict[node_term.Text + "_" + node_subject] += decimal.Parse(node_assessment_sub.Cells[1].Text);
                                }
                                else
                                {
                                    node_assessment_total_Dict[node_term.Text + "_" + node_subject.Text] += decimal.Parse(node_assessment_sub.Cells[1].Text);
                                }
                            }
                        }
                    }
                }
            }

            foreach (DevComponents.AdvTree.Node node_term in advTree1.Nodes)
            {
                foreach (DevComponents.AdvTree.Node node_subject in node_term.Nodes)
                {
                    foreach (DevComponents.AdvTree.Node node_assessment in node_subject.Nodes)
                    {
                        foreach (DevComponents.AdvTree.Node node_assessment_sub in node_assessment.Nodes)
                        {
                            if (node_assessment_sub.Text == "比例:" && node_assessment_total_Dict.ContainsKey(node_term.Text + "_" + node_subject.Text) && node_assessment_total_Dict[node_term.Text + "_" + node_subject.Text] != 0 && decimal.TryParse(node_assessment_sub.Cells[1].Text, out decimal i))
                            {
                                if (isFirstLoad)
                                {
                                    // 去掉尾端括弧之後 加上百分比
                                    node_assessment.Text = node_assessment.Text.Substring(0, node_assessment.Text.Length - 1) + "," + Math.Round(decimal.Parse(node_assessment_sub.Cells[1].Text) * 100 / node_assessment_total_Dict[node_term.Text + "_" + node_subject.Text], 2) + "%)";
                                }
                                else
                                {
                                    // 砍到最後一個逗號, 加上百分比
                                    node_assessment.Text = node_assessment.Text.Substring(0, node_assessment.Text.LastIndexOf(",")) + "," + Math.Round(decimal.Parse(node_assessment_sub.Cells[1].Text) * 100 / node_assessment_total_Dict[node_term.Text + "_" + node_subject.Text], 2) + "%)";
                                }

                            }
                        }
                    }
                }
            }
            #endregion
        }

        //儲存ESL 樣板
        private void btnSave_Click(object sender, EventArgs e)
        {
            string esl_exam_template_id = "" + currentItem.Tag;

            string description_xml = GetXmlDesriptionInTree();


            string xmlStr = "<root>" + description_xml + "</root>";
            XElement elmRoot = XElement.Parse(xmlStr);

            UpdateHelper uh = new UpdateHelper();

            Dictionary<string, string> examWeightDict = GetExamIDWeightDict(elmRoot);

            List<string> rawdataList = new List<string>();

            foreach (string examID in examWeightDict.Keys)
            {
                string insertdata = string.Format(@"
                    SELECT
                        NULL :: BIGINT AS id
                        ,{0}:: INTEGER AS ref_exam_template_id                  
                        ,{1}:: INTEGER AS ref_exam_id
                        ,1  :: BIT AS use_score
                        ,1  :: BIT AS use_text
                        ,{2}:: REAL AS weight
                        ,1  :: BIT AS open_ta
                        ,1  :: BIT AS input_required
                        ,'<Extension><UseScore>是</UseScore><UseEffort>否</UseEffort><UseText>否</UseText><UseAssignmentScore>否</UseAssignmentScore></Extension>'  :: TEXT AS extension
                        ,'INSERT' :: TEXT AS action
                  ", esl_exam_template_id, examID, examWeightDict[examID]);
                rawdataList.Add(insertdata);


            }

            string rawData = string.Join(" UNION ALL", rawdataList);

            string updQuery = @"DELETE 
                FROM te_include
                WHERE ref_exam_template_id = "+ esl_exam_template_id;

            uh.Execute(updQuery);

            updQuery = "WITH ";

            updQuery += string.Format(@"raw_data AS( {0} )", rawData);

            updQuery += string.Format(@"                
                ,insert_data AS
                (   -- 新增 
                INSERT INTO te_include(
                    ref_exam_template_id
                    ,ref_exam_id
                    ,use_score   
                    ,use_text
                    ,weight
                    ,open_ta   
                    ,input_required
                    ,extension
                )
                SELECT         
                    raw_data.ref_exam_template_id:: INTEGER AS ref_exam_template_id   
                    ,raw_data.ref_exam_id:: INTEGER AS ref_exam_id   
                    ,raw_data.use_score::BIT AS use_score   
                    ,raw_data.use_text::BIT AS use_text   
                    ,raw_data.weight::REAL AS weight   
                    ,raw_data.open_ta::BIT AS open_ta   
                    ,raw_data.input_required::BIT AS input_required   
                    ,raw_data.extension::TEXT AS extension       
                FROM
                    raw_data
                WHERE raw_data.action ='INSERT'                                    
                )
  ", esl_exam_template_id);

            updQuery += string.Format(@"
                UPDATE exam_template 
                    SET description = '" + description_xml + "'" +
                    ", extension = '<Extension><ScorePercentage>100</ScorePercentage></Extension>' " +
                "WHERE id = '" + esl_exam_template_id + "'");


            //updQuery = "UPDATE exam_template SET description ='" + description_xml + "',extension ='<Extension><ScorePercentage>100</ScorePercentage></Extension>' WHERE id ='" + esl_exam_template_id + "'";

            //執行sql，更新
            uh.Execute(updQuery);

            lblIsDirty.Visible = false;

            linkLabel1.Enabled = true; // 若有改變儲存，還原。
            linkLabel2.Enabled = true; // 若有改變儲存，還原。
            linkLabel3.Enabled = true; // 若有改變儲存，還原。


            // 同步資料的嘗試，通通失敗...  需要問恩正囉...

            //JHSchool.Evaluation.AssessmentSetup.Instance.SyncAllBackground();            
            //JHSchool.Evaluation.AssessmentSetup.Instance.SyncDataBackground();
            //JHSchool.Evaluation.AssessmentSetup.Instance.WaitLoadingComplete();
            //JHSchool.Evaluation.AssessmentSetup.Instance.SyncData();
            //JHSchool.Evaluation.AssessmentSetup.Instance.SortItems();
            //JHSchool.Evaluation.AssessmentSetup.TestProgram(); ;
            //JHSchool.Evaluation.AssessmentSetup.Instance.SetupPresentation();            
            //JHSchool.Evaluation.TCInstruct.Instance.SyncAllBackground();
            //JHSchool.Evaluation.ScoreCalcRule.Instance.SyncAllBackground();

            MsgBox.Show("儲存樣板成功");

        }

        //將tree畫面上的  資訊 解析出成Xml
        private string GetXmlDesriptionInTree()
        {
            string description_xml = "";

            XmlDocument doc = new XmlDocument();

            XmlElement root = doc.DocumentElement;

            //string.Empty makes cleaner code
            XmlElement element_ESLTemplate = doc.CreateElement(string.Empty, "ESLTemplate", string.Empty);
            doc.AppendChild(element_ESLTemplate);

            foreach (DevComponents.AdvTree.Node term_node in advTree1.Nodes)
            {
                XmlElement element_Term = doc.CreateElement(string.Empty, "Term", string.Empty);
                element_Term.SetAttribute("Name", term_node.Nodes[0].Cells[1].Text);
                element_Term.SetAttribute("Weight", term_node.Nodes[1].Cells[1].Text);
                element_Term.SetAttribute("InputStartTime", term_node.Nodes[2].Cells[1].Text);
                element_Term.SetAttribute("InputEndTime", term_node.Nodes[3].Cells[1].Text);
                element_Term.SetAttribute("Ref_exam_id", _examName_IDDict.ContainsKey(term_node.Nodes[4].Cells[1].Text) ? _examName_IDDict[term_node.Nodes[4].Cells[1].Text] : "");

                foreach (DevComponents.AdvTree.Node subject_node in term_node.Nodes)
                {
                    if (subject_node.TagString == "subject")
                    {
                        XmlElement element_Subject = doc.CreateElement(string.Empty, "Subject", string.Empty);
                        element_Subject.SetAttribute("Name", subject_node.Nodes[0].Cells[1].Text);
                        element_Subject.SetAttribute("Weight", subject_node.Nodes[1].Cells[1].Text);


                        foreach (DevComponents.AdvTree.Node assessment_node in subject_node.Nodes)
                        {
                            if (assessment_node.TagString == "assessment")
                            {
                                XmlElement element_Assessment = doc.CreateElement(string.Empty, "Assessment", string.Empty);

                                element_Assessment.SetAttribute("Name", assessment_node.Nodes[0].Cells[1].Text);
                                element_Assessment.SetAttribute("Weight", assessment_node.Nodes[1].Cells[1].Text);
                                element_Assessment.SetAttribute("TeacherSequence", _teacherRoleCovertRevDict[assessment_node.Nodes[2].Cells[1].Text]);
                                element_Assessment.SetAttribute("Type", _typeCovertRevDict[assessment_node.Nodes[3].Cells[1].Text]);
                                if (_typeCovertRevDict[assessment_node.Nodes[3].Cells[1].Text] == "Comment") // 假如是評語，需要再多存一項 輸入限制(專給Comment 使用)
                                {
                                    element_Assessment.SetAttribute("AllowCustomAssessment", assessment_node.Nodes[4].Cells[1].Text == "是" ? "true" : "false");
                                    element_Assessment.SetAttribute("InputLimit", assessment_node.Nodes[5].Cells[1].Text);
                                }
                                else
                                {
                                    element_Assessment.SetAttribute("AllowCustomAssessment", assessment_node.Nodes[4].Cells[1].Text == "是" ? "true" : "false");
                                }


                                //假如 type 有子項目，其代表indicators
                                if (assessment_node.Nodes[3].Nodes.Count > 0)
                                {
                                    XmlElement element_indicators = doc.CreateElement(string.Empty, "Indicators", string.Empty);

                                    foreach (DevComponents.AdvTree.Node indicators_node in assessment_node.Nodes[3].Nodes)
                                    {
                                        XmlElement element_indicator = doc.CreateElement(string.Empty, "Indicator", string.Empty);

                                        element_indicator.SetAttribute("Name", indicators_node.Nodes[0].Cells[1].Text);
                                        element_indicator.SetAttribute("Description", indicators_node.Nodes[1].Cells[1].Text);

                                        element_indicators.AppendChild(element_indicator);
                                    }

                                    element_Assessment.AppendChild(element_indicators);
                                }

                                element_Subject.AppendChild(element_Assessment);
                            }
                        }
                        element_Term.AppendChild(element_Subject);
                    }
                }

                element_ESLTemplate.AppendChild(element_Term);
            }

            description_xml = doc.OuterXml;

            return description_xml;

        }

        // 新增指標選項
        private void InsertIndicatorSettingNode()
        {
            DevComponents.AdvTree.Node new_indicator_setting_node = new DevComponents.AdvTree.Node();

            new_indicator_setting_node.Tag = "string";
            new_indicator_setting_node.Text = "指標(請輸入名稱)";

            DevComponents.AdvTree.Node new_indicators_node_name = new DevComponents.AdvTree.Node(); //指標名稱
            DevComponents.AdvTree.Node new_indicators_node_description = new DevComponents.AdvTree.Node(); //指標描述

            //項目
            new_indicators_node_name.Text = "名稱:";
            new_indicators_node_description.Text = "描述:";

            //node Tag            
            new_indicators_node_name.Tag = "string";

            //值
            new_indicators_node_name.Cells.Add(new DevComponents.AdvTree.Cell());
            new_indicators_node_description.Cells.Add(new DevComponents.AdvTree.Cell());


            //說明
            new_indicators_node_name.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_indicators_node_name.Tag]));
            new_indicators_node_description.Cells.Add(new DevComponents.AdvTree.Cell("請填入此指標項目的說明，以利評分老師了解。"));


            ////設定為不能點選編輯，避免使用者誤用
            new_indicators_node_name.Cells[0].Editable = false;
            new_indicators_node_name.Cells[2].Editable = false;
            new_indicators_node_description.Cells[0].Editable = false;


            //設定為不能拖曳，避免使用者誤用
            new_indicators_node_name.DragDropEnabled = false;
            new_indicators_node_description.DragDropEnabled = false;


            new_indicator_setting_node.Nodes.Add(new_indicators_node_name);
            new_indicator_setting_node.Nodes.Add(new_indicators_node_description);


            //不可編輯不可、拖曳
            new_indicator_setting_node.Editable = false;
            new_indicator_setting_node.DragDropEnabled = false;

            //加入新增刪除按鈕
            new_indicator_setting_node.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown_InsertDelete);

            node_now.Parent.Nodes.Add(new_indicator_setting_node);
        }

        // 組裝  assessment_node 用
        private DevComponents.AdvTree.Node BuildAssessmentNode()
        {
            // assessment node
            DevComponents.AdvTree.Node new_assessment_node = new DevComponents.AdvTree.Node();

            // 評量(名稱)
            new_assessment_node.Text = "評量(請輸入評量名稱,教師一,0%)";

            //Tag
            new_assessment_node.TagString = "assessment";

            //加入新增刪除按鈕
            new_assessment_node.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown_InsertDelete);


            DevComponents.AdvTree.Node new_assessment_node_name = new DevComponents.AdvTree.Node(); //評量名稱
            DevComponents.AdvTree.Node new_assessment_node_percentage = new DevComponents.AdvTree.Node();   //比例
            DevComponents.AdvTree.Node new_assessment_node_teacherRole = new DevComponents.AdvTree.Node();  //評分老師
            DevComponents.AdvTree.Node new_assessment_node_type = new DevComponents.AdvTree.Node(); //評分種類
            DevComponents.AdvTree.Node new_assessment_node_allowCustomAssessment = new DevComponents.AdvTree.Node(); //是否允許自訂項目


            //項目
            new_assessment_node_name.Text = "名稱:";
            new_assessment_node_percentage.Text = "比例:";
            new_assessment_node_teacherRole.Text = "評分老師";
            new_assessment_node_type.Text = "評分種類";
            new_assessment_node_allowCustomAssessment.Text = "是否允許自訂項目";

            //node Tag
            new_assessment_node_name.Tag = "string";
            new_assessment_node_percentage.Tag = "integer";
            new_assessment_node_teacherRole.Tag = "teacherKind";
            new_assessment_node_type.Tag = "ScoreKind";
            new_assessment_node_allowCustomAssessment.Tag = "AllowCustom";

            //值
            new_assessment_node_name.Cells.Add(new DevComponents.AdvTree.Cell("請輸入評量名稱"));
            new_assessment_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell("0")); //預設為0
            new_assessment_node_teacherRole.Cells.Add(new DevComponents.AdvTree.Cell("教師一")); //預設為教師一
            new_assessment_node_type.Cells.Add(new DevComponents.AdvTree.Cell("分數")); //預設為分數
            new_assessment_node_allowCustomAssessment.Cells.Add(new DevComponents.AdvTree.Cell("否")); //預設為否

            //說明
            new_assessment_node_name.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_name.Tag]));
            new_assessment_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_percentage.Tag]));
            new_assessment_node_teacherRole.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_teacherRole.Tag]));
            new_assessment_node_type.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_type.Tag]));
            new_assessment_node_allowCustomAssessment.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_assessment_node_allowCustomAssessment.Tag]));

            // 點擊事件 (適用於:teacherKind、ScoreKind、AllowCustom)
            new_assessment_node_teacherRole.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown);
            new_assessment_node_type.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown);
            new_assessment_node_allowCustomAssessment.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown);

            //設定為不能點選編輯，避免使用者誤用
            new_assessment_node_name.Cells[0].Editable = false;
            new_assessment_node_name.Cells[2].Editable = false;
            new_assessment_node_percentage.Cells[0].Editable = false;
            new_assessment_node_percentage.Cells[2].Editable = false;
            new_assessment_node_teacherRole.Cells[0].Editable = false;
            new_assessment_node_teacherRole.Cells[2].Editable = false;
            new_assessment_node_type.Cells[0].Editable = false;
            new_assessment_node_type.Cells[2].Editable = false;
            new_assessment_node_allowCustomAssessment.Cells[0].Editable = false;
            new_assessment_node_allowCustomAssessment.Cells[2].Editable = false;

            //設定為不能拖曳，避免使用者誤用
            new_assessment_node_name.DragDropEnabled = false;
            new_assessment_node_percentage.DragDropEnabled = false;
            new_assessment_node_teacherRole.DragDropEnabled = false;
            new_assessment_node_type.DragDropEnabled = false;
            new_assessment_node_allowCustomAssessment.DragDropEnabled = false;


            //加入子 node
            new_assessment_node.Nodes.Add(new_assessment_node_name);
            new_assessment_node.Nodes.Add(new_assessment_node_percentage);
            new_assessment_node.Nodes.Add(new_assessment_node_teacherRole);
            new_assessment_node.Nodes.Add(new_assessment_node_type);
            new_assessment_node.Nodes.Add(new_assessment_node_allowCustomAssessment);

            //設定為不能點選編輯，避免使用者誤用
            new_assessment_node.Cells[0].Editable = false;

            return new_assessment_node;
        }

        // 組裝  subject_node 用
        private DevComponents.AdvTree.Node BuildSubjectNode()
        {
            // subject node
            DevComponents.AdvTree.Node new_subjet_node = new DevComponents.AdvTree.Node();

            // 科目(名稱)
            new_subjet_node.Text = "科目(請輸入科目名稱,0%)";

            // Tag
            new_subjet_node.TagString = "subject";

            //加入新增刪除按鈕
            new_subjet_node.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown_InsertDelete);

            DevComponents.AdvTree.Node new_subject_node_name = new DevComponents.AdvTree.Node(); //科目名稱
            DevComponents.AdvTree.Node new_subject_node_percentage = new DevComponents.AdvTree.Node(); //比例

            //項目
            new_subject_node_name.Text = "名稱:";
            new_subject_node_percentage.Text = "比例:";
            //node Tag
            new_subject_node_name.Tag = "string";
            new_subject_node_percentage.Tag = "integer";

            //值
            new_subject_node_name.Cells.Add(new DevComponents.AdvTree.Cell("請輸入科目名稱"));
            new_subject_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell("0"));

            //說明
            new_subject_node_name.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_subject_node_name.Tag]));
            new_subject_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_subject_node_percentage.Tag]));

            //設定為不能點選編輯，避免使用者誤用
            new_subject_node_name.Cells[0].Editable = false;
            new_subject_node_name.Cells[2].Editable = false;
            new_subject_node_percentage.Cells[0].Editable = false;
            new_subject_node_percentage.Cells[2].Editable = false;

            //設定為不能拖曳，避免使用者誤用
            new_subject_node_name.DragDropEnabled = false;
            new_subject_node_percentage.DragDropEnabled = false;

            // 將子node 加入
            new_subjet_node.Nodes.Add(new_subject_node_name);
            new_subjet_node.Nodes.Add(new_subject_node_percentage);

            //設定為不能點選編輯，避免使用者誤用
            new_subjet_node.Cells[0].Editable = false;

            return new_subjet_node;
        }

        // 組裝  term_node 用
        private DevComponents.AdvTree.Node BuildTermNode()
        {
            // term node
            DevComponents.AdvTree.Node new_term_node = new DevComponents.AdvTree.Node();
            // 試別(名稱)
            new_term_node.Text = "試別(請輸入試別名稱,)";
            // Tag
            new_term_node.TagString = "term";

            //加入新增刪除按鈕
            new_term_node.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown_InsertDelete);

            //設定為不能點選編輯，避免使用者誤用
            new_term_node.Cells[0].Editable = false;

            //設定為不能拖曳，避免使用者誤用
            new_term_node.DragDropEnabled = false;

            DevComponents.AdvTree.Node new_term_node_name = new DevComponents.AdvTree.Node(); //試別名稱
            DevComponents.AdvTree.Node new_term_node_percentage = new DevComponents.AdvTree.Node(); //比例
            DevComponents.AdvTree.Node new_term_node_inputStartTime = new DevComponents.AdvTree.Node(); //輸入開始時間
            DevComponents.AdvTree.Node new_term_node_inputEndTime = new DevComponents.AdvTree.Node(); //輸入結束時間
            DevComponents.AdvTree.Node new_term_node_refExamID = new DevComponents.AdvTree.Node(); //對應系統試別名稱

            //項目
            new_term_node_name.Text = "名稱:";
            new_term_node_percentage.Text = "比例:";
            new_term_node_inputStartTime.Text = "成績輸入開始時間:";
            new_term_node_inputEndTime.Text = "成績輸入截止時間:";
            new_term_node_refExamID.Text = "對應系統試別:";

            //node Tag
            new_term_node_name.Tag = "string";
            new_term_node_percentage.Tag = "integer";
            new_term_node_inputStartTime.Tag = "time";
            new_term_node_inputEndTime.Tag = "time";
            new_term_node_refExamID.Tag = "Exam";

            //值
            new_term_node_name.Cells.Add(new DevComponents.AdvTree.Cell("請輸入試別名稱"));
            new_term_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell("0"));
            new_term_node_inputStartTime.Cells.Add(new DevComponents.AdvTree.Cell());
            new_term_node_inputEndTime.Cells.Add(new DevComponents.AdvTree.Cell());
            new_term_node_refExamID.Cells.Add(new DevComponents.AdvTree.Cell());

            //說明
            new_term_node_name.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_term_node_name.Tag]));
            new_term_node_percentage.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_term_node_percentage.Tag]));
            new_term_node_inputStartTime.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_term_node_inputStartTime.Tag]));
            new_term_node_inputEndTime.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_term_node_inputEndTime.Tag]));

            //試別有值才給存，無值，標紅色
            if (new_term_node_refExamID.Cells[1].Text != "")
            {
                new_term_node_refExamID.Cells.Add(new DevComponents.AdvTree.Cell(_hintGuideDict["" + new_term_node_refExamID.Tag]));
                btnSave.Enabled = true;
            }
            else
            {
                new_term_node_refExamID.Cells.Add(new DevComponents.AdvTree.Cell("<b><font color=\"#ED1C24\">" + _hintGuideDict["" + new_term_node_refExamID.Tag] + "</font></b>")); //輸入規則錯誤，顯示紅字                
                btnSave.Enabled = false;
            }

            //點擊事件
            new_term_node_refExamID.NodeMouseDown += new System.Windows.Forms.MouseEventHandler(NodeMouseDown);

            //設定為不能點選編輯，避免使用者誤用
            new_term_node_name.Cells[0].Editable = false;
            new_term_node_name.Cells[2].Editable = false;
            new_term_node_percentage.Cells[0].Editable = false;
            new_term_node_percentage.Cells[2].Editable = false;
            new_term_node_inputStartTime.Cells[0].Editable = false;
            new_term_node_inputStartTime.Cells[2].Editable = false;
            new_term_node_inputEndTime.Cells[0].Editable = false;
            new_term_node_inputEndTime.Cells[2].Editable = false;
            new_term_node_refExamID.Cells[0].Editable = false;
            new_term_node_refExamID.Cells[2].Editable = false;

            //設定為不能拖曳，避免使用者誤用
            new_term_node_name.DragDropEnabled = false;
            new_term_node_percentage.DragDropEnabled = false;
            new_term_node_inputStartTime.DragDropEnabled = false;
            new_term_node_inputEndTime.DragDropEnabled = false;
            new_term_node_refExamID.DragDropEnabled = false;

            //將子node 加入
            new_term_node.Nodes.Add(new_term_node_name);
            new_term_node.Nodes.Add(new_term_node_percentage);
            new_term_node.Nodes.Add(new_term_node_inputStartTime);
            new_term_node.Nodes.Add(new_term_node_inputEndTime);
            new_term_node.Nodes.Add(new_term_node_refExamID);

            return new_term_node;
        }

        private void IsDirtyOrNot()
        {
            //檢查是否與原資料相同，若有改變，則顯示"未儲存" 提醒使用者要儲存
            if (_oriTemplateDescriptionDict["" + currentItem.Tag] != GetXmlDesriptionInTree())
            {
                lblIsDirty.Visible = true;

                linkLabel1.Enabled = false; // 若有改變尚未儲存，則不給設定報表樣版。
                linkLabel2.Enabled = false; // 若有改變尚未儲存，則不給設定報表樣版。
                linkLabel3.Enabled = false; // 若有改變尚未儲存，則不給設定報表樣版。
            }
            else
            {
                lblIsDirty.Visible = false;

                linkLabel1.Enabled = true; // 若有改變儲存，還原。
                linkLabel2.Enabled = true; // 若有改變儲存，還原。
                linkLabel3.Enabled = true; // 若有改變儲存，還原。
            }
        }

        // 檢查是否在同一層資料結構有重覆的資料
        private bool CheckDuplicated()
        {
            bool duplicated = false;

            List<string> checkDuplicatedList = new List<string>();

            if (node_now.Parent.TagString != "term")
            {
                // assessment subject  在此處理
                foreach (DevComponents.AdvTree.Node node in node_now.Parent.Parent.Nodes)
                {
                    foreach (DevComponents.AdvTree.Node node2 in node.Nodes)
                    {
                        if (node2.Text == "名稱:")
                        {
                            if (!checkDuplicatedList.Contains(node2.Cells[1].Text))
                            {
                                checkDuplicatedList.Add(node2.Cells[1].Text);
                            }
                            else
                            {
                                duplicated = true;
                            }
                        }
                    }
                }
            }
            else
            {
                // term 在此處理
                foreach (DevComponents.AdvTree.Node node in advTree1.Nodes)
                {
                    foreach (DevComponents.AdvTree.Node node2 in node.Nodes)
                    {
                        if (node2.Text == "名稱:")
                        {
                            if (!checkDuplicatedList.Contains(node2.Cells[1].Text))
                            {
                                checkDuplicatedList.Add(node2.Cells[1].Text);
                            }
                            else
                            {
                                duplicated = true;
                            }
                        }
                        if (node2.Text == "對應系統試別:")
                        {
                            if (!checkDuplicatedList.Contains(node2.Cells[1].Text))
                            {
                                checkDuplicatedList.Add(node2.Cells[1].Text);
                            }
                            else
                            {
                                duplicated = true;
                            }
                        }
                    }
                }
            }

            return duplicated;
        }

        //刪除樣板
        private void btnDelete_Click(object sender, EventArgs e)
        {
            try
            {
                if (currentItem == null) return;

                string msg = "確定要刪除「" + currentItem.Text + "」評量設定？\n";
                msg += "刪除後，使用此評量設定的「課程」將會自動變成未設定評量設定狀態。";

                DialogResult dr = MsgBox.Show(msg, Application.ProductName, MessageBoxButtons.YesNo);

                if (dr == DialogResult.Yes)
                {
                    string esl_exam_template_id = "" + currentItem.Tag;

                    UpdateHelper uh = new UpdateHelper();

                    //依照所選項目刪除
                    string updQuery = "DELETE FROM exam_template WHERE id ='" + esl_exam_template_id + "'";

                    //執行sql，更新
                    uh.Execute(updQuery);

                    MsgBox.Show("刪除樣板成功");

                    // renew UI畫面
                    BeforeLoadAssessmentSetup();
                    LoadAssessmentSetups();
                }
            }
            catch (Exception ex)
            {
                MsgBox.Show(ex.Message);
                //CurrentUser.ReportError(ex);
            }

        }

        //新增樣板
        private void btnAddNew_Click(object sender, EventArgs e)
        {
            InsertNewTemplateForm editor = new InsertNewTemplateForm();
            DialogResult dr = editor.ShowDialog();

            //新增過後，畫面renew
            if (dr == DialogResult.OK)
            {
                try
                {
                    BeforeLoadAssessmentSetup();
                    LoadAssessmentSetups();
                }
                catch (Exception ex)
                {
                    MsgBox.Show(ex.Message);
                }
            }
        }

        //期中報表樣板
        private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ReportTemplateSettingForm rtsf = new ReportTemplateSettingForm();

            rtsf.SourceID = "" + currentItem.Tag;
            rtsf.SourceType = "期中";

            rtsf.ShowDialog();
        }

        // 期末報表樣板
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ReportTemplateSettingForm rtsf = new ReportTemplateSettingForm();

            rtsf.SourceID = "" + currentItem.Tag;
            rtsf.SourceType = "期末";

            rtsf.ShowDialog();

        }

        // 學期報表樣板
        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ReportTemplateSettingForm rtsf = new ReportTemplateSettingForm();

            rtsf.SourceID = "" + currentItem.Tag;
            rtsf.SourceType = "學期";

            rtsf.ShowDialog();
        }

        // 當使用者按住  Shift  或是 Alt  在雙擊滑鼠時， 啟動 隱藏的匯入匯出 設定 Xml 功能
        private void peTemplateName1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == (Keys.Shift | Keys.Alt))
            {
                ExportXmlBtn.Visible = true;
                importXmlBtn.Visible = true;
            }
        }


        // 匯出 設定 Xml 檔案
        private void ExportXmlBtn_Click(object sender, EventArgs e)
        {
            string description_xml = GetXmlDesriptionInTree();

            XmlDocument doc = new XmlDocument();

            doc.LoadXml(description_xml);

            doc.PreserveWhitespace = true;

            SaveFileDialog saveDialog = new SaveFileDialog();

            //saveDialog.Filter = "*.xml|all Files(*.*)|*.*";
            saveDialog.Filter = "XML files(.xml)|*.xml|all Files(*.*)|*.*";
            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    doc.Save(saveDialog.FileName);
                    System.Diagnostics.Process.Start(saveDialog.FileName);
                }
                catch
                {
                    MsgBox.Show("路徑無法存取，請確認檔案是否未正確關閉。");
                }
            }
        }


        // 匯入 設定 Xml 檔案
        private void importXmlBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "選擇檔案";
            //ofd.Filter = "*.xml|所有檔案 (*.*)|*.* ";
            ofd.Multiselect = true;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string esl_exam_template_id = "" + currentItem.Tag;

                XmlDocument doc = new XmlDocument();

                doc.Load(ofd.FileName);

                string description_xml = doc.OuterXml;

                UpdateHelper uh = new UpdateHelper();

                //依照所選項目儲存
                string updQuery = "UPDATE exam_template SET description ='" + description_xml + "' WHERE id ='" + esl_exam_template_id + "'";

                //執行sql，更新
                uh.Execute(updQuery);

                MsgBox.Show("上傳樣板XML設定成功");
            }
            else
            {
                MsgBox.Show("未選擇檔案!!");
            }
        }




        // 自 評分樣版設定抓取 試別id 對應的比重
        private Dictionary<string, string> GetExamIDWeightDict(XElement elmRoot)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            

            if (elmRoot != null)
            {
                if (elmRoot.Element("ESLTemplate") != null)
                {
                    foreach (XElement ele_term in elmRoot.Element("ESLTemplate").Elements("Term"))
                    {
                        string refExamId = "";
                        string weight = "";

                        refExamId = ele_term.Attribute("Ref_exam_id") != null ? ele_term.Attribute("Ref_exam_id").Value : "";
                        weight = ele_term.Attribute("Weight") != null ? ele_term.Attribute("Weight").Value : "";

                        if (!dict.ContainsKey(refExamId))
                        {
                            dict.Add(refExamId, weight);
                        }
                    }
                }
            }




            return dict;
        }


    }
}

