﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FISCA;
using FISCA.Presentation;
using K12.Presentation;
using FISCA.Permission;
using JHSchool;


namespace ESL_System
{
    public class Program
    {
        //2018/4/11 穎驊因應康橋英文系統、弘文ESL 專案 ，開始建構教務作業ESL 評分樣版設定
        [FISCA.MainMethod()]
        public static void Main()
        {
            FISCA.UDT.AccessHelper _AccessHelper = new FISCA.UDT.AccessHelper();

            _AccessHelper.Select<UDT_ReportTemplate>(); // 先將UDT 選起來，如果是第一次開啟沒有話就會新增

            Catalog ribbon = RoleAclSource.Instance["教務作業"]["功能按鈕"];
            ribbon.Add(new RibbonFeature("ESL評分樣版設定", "ESL評分樣版設定"));

            MotherForm.RibbonBarItems["教務作業", "基本設定"]["設定"]["ESL評分樣版設定"].Enable = UserAcl.Current["ESL評分樣版設定"].Executable;

            MotherForm.RibbonBarItems["教務作業", "基本設定"]["設定"]["ESL評分樣版設定"].Click += delegate
            {
                Form.ESL_TemplateSetupManager form = new Form.ESL_TemplateSetupManager();

                form.ShowDialog();

            };

            Catalog ribbon2 = RoleAclSource.Instance["課程"]["ESL課程"];
            ribbon2.Add(new RibbonFeature("ESL評量分數計算", "評量成績結算"));

            MotherForm.RibbonBarItems["課程", "ESL課程"]["評量成績結算"].Enable = false;

            K12.Presentation.NLDPanels.Course.SelectedSourceChanged += (sender, e) =>
            {
                if (K12.Presentation.NLDPanels.Course.SelectedSource.Count > 0)
                {
                    MotherForm.RibbonBarItems["課程", "ESL課程"]["評量成績結算"].Enable = UserAcl.Current["ESL評量分數計算"].Executable;
                }
                else
                {
                    MotherForm.RibbonBarItems["課程", "ESL課程"]["評量成績結算"].Enable = false;
                }
            };

            MotherForm.RibbonBarItems["課程", "ESL課程"]["評量成績結算"].Image = Properties.Resources.calc_64;
            MotherForm.RibbonBarItems["課程", "ESL課程"]["評量成績結算"].Size = RibbonBarButton.MenuButtonSize.Medium;

            MotherForm.RibbonBarItems["課程", "ESL課程"]["評量成績結算"].Click += delegate
            {
                Form.CheckCalculateTermForm form = new Form.CheckCalculateTermForm(K12.Presentation.NLDPanels.Course.SelectedSource);

                form.ShowDialog();

            };

            Catalog ribbon3 = RoleAclSource.Instance["課程"]["ESL報表"];
            ribbon3.Add(new RibbonFeature("ESL成績單", "ESL報表"));

            MotherForm.RibbonBarItems["課程", "資料統計"]["報表"]["ESL報表"]["ESL成績單"].Enable = UserAcl.Current["ESL成績單"].Executable && K12.Presentation.NLDPanels.Course.SelectedSource.Count > 0;

            K12.Presentation.NLDPanels.Course.SelectedSourceChanged += delegate
            {
                MotherForm.RibbonBarItems["課程", "資料統計"]["報表"]["ESL報表"]["ESL成績單"].Enable = UserAcl.Current["ESL成績單"].Executable && (K12.Presentation.NLDPanels.Course.SelectedSource.Count > 0);
            };


            MotherForm.RibbonBarItems["課程", "資料統計"]["報表"]["ESL報表"]["ESL成績單"].Click += delegate
            {
                List<string> esl_couse_list = K12.Presentation.NLDPanels.Course.SelectedSource.ToList();

                Form.PrintESLReportForm printform = new Form.PrintESLReportForm(esl_couse_list);

                printform.ShowDialog();

            };

            



        }
    }
}
