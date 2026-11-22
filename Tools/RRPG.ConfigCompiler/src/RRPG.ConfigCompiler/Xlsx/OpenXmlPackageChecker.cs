using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace RRPG.ConfigCompiler.Xlsx
{
    /// <summary>
    /// Open XML SDK 包结构校验（独立于 ClosedXML 的第二意见）。
    ///
    /// 用途：Excel UX Gate —— 证明生成 / 保存后的 .xlsx 是结构合法的 OOXML 包，
    /// 而不是“ClosedXML 自己能读回来”而已。
    /// </summary>
    public static class OpenXmlPackageChecker
    {
        /// <summary>返回结构错误描述列表（空列表 = 合法）。</summary>
        public static List<string> Validate(string path)
        {
            var problems = new List<string>();

            try
            {
                using (var document = SpreadsheetDocument.Open(path, false))
                {
                    var validator = new OpenXmlValidator();
                    foreach (var error in validator.Validate(document))
                    {
                        problems.Add(error.Description
                            + " @ " + (error.Path != null ? error.Path.XPath : "unknown"));
                    }
                }
            }
            catch (Exception ex)
            {
                problems.Add("无法作为 OOXML 包打开：" + ex.GetType().Name + ": " + ex.Message);
            }

            return problems;
        }
    }
}
