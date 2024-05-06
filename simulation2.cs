using Microsoft.EntityFrameworkCore;
using PDCProjectApi.Common;
using PDCProjectApi.Data;
using System.Collections.Concurrent;
using PDCProjectApi.Common.Function;
using System.Data;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PDCProjectApi.Model.View;
using System.Globalization;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Office.CustomUI;

namespace PDCProjectApi.Services
{
    public interface ISimulation
    {
        Task ExportPartMaster(string product, bool changepp);
        Task ExportLivePp10Minute(string product);
        Task ExportLivePp10MinuteWithChangepp(string product);
        Task ExportCountlineByStructure(string product);
        Task ExportParallel(string product);
        Task PushLivePp10Minute(string product);
        Task PushLivePp10MinuteWithChangepp(string product);
        Task PushCountlineByStructure(string product);
        Task PushPartMaster(string product);
        Task ExportLeadtime(string product);
        Task ExportOp5NpisDoPo();
        Task PushOp5NpisDoPo();
        Task PushBlockTimeByDate(string product);
        Task PushPartMasterChangePP(string product);
        Task PushLeadtime();
    }
    public class SimulationJob: ISimulation
    {
        private readonly PdcsystemContext _ctx;
        private readonly object lockObj = new object();
        private readonly IEmailService email;
        private readonly List<string> lstMailReceive = new List<string>() { };
        private readonly IGlobalVariable global;
        private List<string> lstBcUse = new List<string>();
        private readonly string outputPathOri = "";
        private readonly DateTime[] lstBlockTimeAll;
        private readonly PcSimulationMasterBlockTime[] lstBlockTimeSimple;
        public SimulationJob(PdcsystemContext _dbContext, IEmailService mail, IGlobalVariable gl)
        {
            this._ctx = _dbContext;
            this.email = mail;
            this.global = gl;
            this.lstBcUse = this.global.ReturnBlockCode();
            this.lstMailReceive = this.global.ReturnPDCMail();
            this.outputPathOri = this.global.ReturnPathOutput();
            this.lstBlockTimeAll = new DateTime[1320];
            var check = this._ctx.PcSimulationDetailBlocktimeBydates.Where(x => x.Active == true && x.CreatedDate != null && x.CreatedDate > DateTime.Today).OrderByDescending(x => x.CreatedDate).FirstOrDefault();
            if(check != null)
            {
                this.lstBlockTimeAll = check.BlockTime;
            }
            this.lstBlockTimeSimple = this._ctx.PcSimulationMasterBlockTimes.Where(x => x.Active == true).OrderBy(x => x.OrderTime).ToArray();
        }
        public async Task ExportPartMaster(string product, bool changepp)
        {
            var header = await _ctx.TodStructureOutputHeaders.Where(x => x.Active == true && x.Product == product)
                .Select(x => new { Model = x.Model, Merchandise = x.Merchandise, MerCode = x.MerCode })
                .AsNoTracking()
                .FirstAsync();
            var lstStructure = new List<PcSimulationPartMasterLbp>();
            if (product == "IJ" && changepp == false)
            {
                var lstStructure1 = await _ctx.PcSimulationPartMasterIjs.Where(x => x.Active == true).ToListAsync();
                lstStructure = lstStructure1.MapSimulation();
            }
            else if (product == "LBP" && changepp == false)
            {
                lstStructure = await _ctx.PcSimulationPartMasterLbps.Where(x => x.Active == true).ToListAsync();
            }
            else if (product == "IJ" && changepp == true)
            {
                var lstStructure1 = await _ctx.PcSimulationPartMasterChangeppIjs.Where(x => x.Active == true).ToListAsync();
                lstStructure = lstStructure1.MapSimulation();
            }
            else if (product == "LBP" && changepp == true)
            {
                var lstStructure1 = await _ctx.PcSimulationPartMasterChangeppLbps.Where(x => x.Active == true).ToListAsync();
                lstStructure = lstStructure1.MapSimulationP();
            }
            if(lstStructure.Count < 1)
            {
                return;
            }
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (ExcelPackage excelPackage = new ExcelPackage())
            {
                ExcelWorksheet excelWorksheet;
                excelWorksheet = excelPackage.Workbook.Worksheets.Add("Part Master" + (changepp ? "WithChangePP": ""));
                var numberRowTotal = lstStructure.Count;
                if(numberRowTotal < 1)
                {
                    excelPackage.Dispose();
                    return;
                }
                int currentRow = 8;
                var numberColumn1 = lstStructure.First().Usage.Length;
                var numberColumn2 = lstBlockTimeAll.Count();
                for (int i = 0; i < numberRowTotal; i++)
                {
                    var strucItem = lstStructure[i];
                    excelWorksheet.Cells["A" + currentRow].Value = (i + 1);
                    excelWorksheet.Cells["B" + currentRow].Value = strucItem.PartnoBc;
                    excelWorksheet.Cells["C" + currentRow].Value = strucItem.PartNo;
                    excelWorksheet.Cells["D" + currentRow].Value = strucItem.Dim;
                    excelWorksheet.Cells["E" + currentRow].Value = strucItem.Pr;
                    excelWorksheet.Cells["F" + currentRow].Value = strucItem.PartName;
                    excelWorksheet.Cells["G" + currentRow].Value = strucItem.Vendor;
                    excelWorksheet.Cells["H" + currentRow].Value = strucItem.Unit;
                    excelWorksheet.Cells["I" + currentRow].Value = strucItem.Model;
                    excelWorksheet.Cells["J" + currentRow].Value = strucItem.Destination;
                    excelWorksheet.Cells["K" + currentRow].Value = strucItem.Merchandise;
                    excelWorksheet.Cells["L" + currentRow].Value = strucItem.Factory;
                    excelWorksheet.Cells["M" + currentRow].Value = strucItem.Ratio;
                    excelWorksheet.Cells["N" + currentRow].Value = strucItem.EffectivedateChange;
                    excelWorksheet.Cells["O" + currentRow].Value = strucItem.RatioChange;
                    excelWorksheet.Cells["P" + currentRow].Value = strucItem.DoPic;
                    excelWorksheet.Cells["Q" + currentRow].Value = strucItem.PoPic;
                    excelWorksheet.Cells["R" + currentRow].Value = strucItem.Pair;
                    for (int j = 0; j < numberColumn1; j++)
                    {
                        try
                        {
                            if (strucItem.Usage[j] != 0)
                            {
                                int columnCurrent = 19 + j;
                                excelWorksheet.Cells[currentRow, columnCurrent].Value = strucItem.Usage[j];
                            }
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                    //tìm những usage > 0
                    try
                    {
                        for (int t = 0; t < strucItem.PpValue.Length; t++)
                        {
                            double valueAt = strucItem.PpValue[t];
                            if (valueAt != 0)
                            {
                                excelWorksheet.Cells[currentRow, t + 19 + numberColumn1].Value = valueAt;
                                excelWorksheet.Cells[currentRow, t + 19 + numberColumn1].Style.Numberformat.Format = "#,##0.0";
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }
                    currentRow++;
                }

                excelWorksheet.Cells[5, 1, numberRowTotal + 7, 18 + numberColumn1 + numberColumn2].Style.Font.Size = 12;
                excelWorksheet.Cells[5, 1, numberRowTotal + 7, 18 + numberColumn1 + numberColumn2].Style.Font.Name = "Calibri";
                excelWorksheet.Cells[5, 1, numberRowTotal + 7, 18 + numberColumn1 + numberColumn2].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[5, 1, numberRowTotal + 7, 18 + numberColumn1 + numberColumn2].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[5, 1, numberRowTotal + 7, 18 + numberColumn1 + numberColumn2].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[5, 1, numberRowTotal + 7, 18 + numberColumn1 + numberColumn2].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                {
                    excelWorksheet.Cells["A5"].Value = "STT";
                    excelWorksheet.Cells["A5:A7"].Merge = true;
                    excelWorksheet.Cells["B5"].Value = "Partno&Bc";
                    excelWorksheet.Cells["B5:B7"].Merge = true;
                    excelWorksheet.Cells["C5"].Value = "PartNo";
                    excelWorksheet.Cells["C5:C7"].Merge = true;
                    excelWorksheet.Cells["D5"].Value = "Dim";
                    excelWorksheet.Cells["D5:D7"].Merge = true;
                    excelWorksheet.Cells["E5"].Value = "Pr";
                    excelWorksheet.Cells["E5:E7"].Merge = true;
                    excelWorksheet.Cells["F5"].Value = "Part Name";
                    excelWorksheet.Cells["F5:F7"].Merge = true;
                    excelWorksheet.Cells["G5"].Value = "Bc";
                    excelWorksheet.Cells["G5:G7"].Merge = true;
                    excelWorksheet.Cells["H5"].Value = "Unit";
                    excelWorksheet.Cells["H5:H7"].Merge = true;
                    excelWorksheet.Cells["I5"].Value = "Model";
                    excelWorksheet.Cells["I5:I7"].Merge = true;
                    excelWorksheet.Cells["J5"].Value = "Destination";
                    excelWorksheet.Cells["J5:J7"].Merge = true;
                    excelWorksheet.Cells["K5"].Value = "Merchandise";
                    excelWorksheet.Cells["K5:K7"].Merge = true;
                    excelWorksheet.Cells["L5"].Value = "Factory";
                    excelWorksheet.Cells["L5:L7"].Merge = true;
                    excelWorksheet.Cells["M5"].Value = "Ratio";
                    excelWorksheet.Cells["M5:M7"].Merge = true;
                    excelWorksheet.Cells["N5:O5"].Merge = true;
                    excelWorksheet.Cells["N5"].Value = "Ratio Change";
                    excelWorksheet.Cells["N6"].Value = "EffectiveDate";
                    excelWorksheet.Cells["O6"].Value = "Ratio";
                    excelWorksheet.Cells["P7"].Value = "Do Pic";
                    excelWorksheet.Cells["Q7"].Value = "Po Pic";
                    excelWorksheet.Cells["R5"].Value = "Model";
                    excelWorksheet.Cells["R6"].Value = "Mername";
                    excelWorksheet.Cells["R7"].Value = "Mer code";
                    excelWorksheet.Cells["A5:O7"].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    excelWorksheet.Cells["A5:O7"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Orange);
                    excelWorksheet.Cells[5, 19, 7, 18 + numberColumn1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    excelWorksheet.Cells[5, 19, 7, 18 + numberColumn1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LawnGreen);
                    excelWorksheet.Cells[7, 19, 7, 18 + numberColumn1].Style.TextRotation = 90;
                    excelWorksheet.Cells[5, 19 + numberColumn1, 7, 19 + numberColumn1 + numberColumn2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    excelWorksheet.Cells[5, 19 + numberColumn1, 7, 19 + numberColumn1 + numberColumn2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.AliceBlue);
                    excelWorksheet.Cells[5, 19 + numberColumn1, 7, 19 + numberColumn1 + numberColumn2].Style.TextRotation = 90;
                    var arrStructureHeader5 = header.Model;
                    var arrStructureHeader6 = header.Merchandise;
                    var arrStructureHeader7 = header.MerCode;
                    for (int t = 19; t < 19 + numberColumn1; t++)
                    {
                        excelWorksheet.Cells[5, t].Value = arrStructureHeader5[t - 19].Split(':')[1];
                        excelWorksheet.Cells[6, t].Value = arrStructureHeader6[t - 19].Split(':')[1];
                        excelWorksheet.Cells[7, t].Value = arrStructureHeader7[t - 19].Split(':')[1];
                    }
                    var startHeaderTimeIndex = 19 + numberColumn1;
                    for (int t = startHeaderTimeIndex; t < startHeaderTimeIndex + numberColumn2; t++)
                    {
                        excelWorksheet.Cells[6, t].Value = lstBlockTimeAll[t - startHeaderTimeIndex].ToString("d-MMM");
                        excelWorksheet.Cells[7, t].Value = lstBlockTimeAll[t - startHeaderTimeIndex].ToString("HH:mm");
                    }
                }

                string outputPath = this.outputPathOri + @"\7. Digital Simulation\Output\OP2.Simulation" + (changepp ? "WithChangePP" : "") + "_" + product + "." + DateTime.Now.ToString("yyyy-MM-dd HHmm") + " Part master.xlsx";
                FileInfo excelFile = new FileInfo(outputPath);
                excelPackage.SaveAs(excelFile);
                excelPackage.Dispose();


                email.Initial(lstMailReceive, " Completed Simulation part " + product + " output", @"<p style='font-weight: bold;'>Dear [Mrs/Mr]. All,</p>
                <p>You have received a notification about <span style='font-weight: bold; color: red; font-style: italic;'>Trial EXPORT Simulation part - output Part master </span>from PSI System. Please check at <a href='file:" + this.outputPathOri + @"\7. Digital Simulation\Output'>Export Folder</a> .</p>
                
                ");
            }
        }
        public async Task ExportCountlineByStructure(string product)
        {
            var lstStructure = new List<PcSimulationCountlineLbp>();

            if (product == "LBP")
            {
                lstStructure = await _ctx.PcSimulationCountlineLbps.Where(x => x.Active == true).ToListAsync();
            }
            else if (product == "IJ")
            {
                var lstStructure1 = await _ctx.PcSimulationCountlineIjs.Where(x => x.Active == true).ToListAsync();
                lstStructure = lstStructure1.MapSimulation();
            }
            if(lstStructure.Count < 1)
            {
                return;
            }
            var cColumn = lstBlockTimeAll.Length;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (ExcelPackage excelPackage = new ExcelPackage())
            {
                ExcelWorksheet excelWorksheet;
                excelWorksheet = excelPackage.Workbook.Worksheets.Add("Countline");
                var numberRowTotal = lstStructure.Count;
                if (numberRowTotal < 1)
                {
                    excelPackage.Dispose();
                    return;
                }
                int currentRow = 9;
                var numberColumn2 = cColumn;
                for (int i = 0; i < numberRowTotal; i++)
                {
                    var strucItem = lstStructure[i];
                    excelWorksheet.Cells["A" + currentRow].Value = (i + 1);
                    excelWorksheet.Cells["B" + currentRow].Value = "";
                    excelWorksheet.Cells["C" + currentRow].Value = strucItem.PartNo;
                    excelWorksheet.Cells["D" + currentRow].Value = strucItem.Vendor;
                    excelWorksheet.Cells["E" + currentRow].Value = strucItem.Ratio;
                    excelWorksheet.Cells["F" + currentRow].Value = strucItem.Model;
                    excelWorksheet.Cells["G" + currentRow].Value = strucItem.Merchandise;

                    try
                    {
                        if (strucItem.CountValue.Length > 0)
                        {
                            for (int j = 8; j < strucItem.CountValue.Length + 8; j++)
                            {
                                //var dModel = lstTime[j - 8];
                                excelWorksheet.Cells[currentRow, j].Value = strucItem.CountValue[j - 8];
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }
                    Console.WriteLine("Simulation countline " + currentRow);
                    currentRow++;
                }

                excelWorksheet.Cells[8, 1, numberRowTotal + 8, 7 + numberColumn2].Style.Font.Size = 12;
                excelWorksheet.Cells[8, 1, numberRowTotal + 8, 7 + numberColumn2].Style.Font.Name = "Calibri";
                excelWorksheet.Cells[8, 1, numberRowTotal + 8, 7 + numberColumn2].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[8, 1, numberRowTotal + 8, 7 + numberColumn2].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[8, 1, numberRowTotal + 8, 7 + numberColumn2].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[8, 1, numberRowTotal + 8, 7 + numberColumn2].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells["A8"].Value = "STT";
                excelWorksheet.Cells["B8"].Value = "";
                excelWorksheet.Cells["C8"].Value = "Part No";
                excelWorksheet.Cells["D8"].Value = "Vendor";
                excelWorksheet.Cells["E8"].Value = "Ratio";
                excelWorksheet.Cells["F8"].Value = "Model";
                excelWorksheet.Cells["G8"].Value = "Merchandise";
                for (int i = 8; i < cColumn + 8; i++)
                {
                    var dModel = lstBlockTimeAll[i - 8];
                    excelWorksheet.Cells[7, i].Value = dModel.ToString("d-MMM");
                    excelWorksheet.Cells[8, i].Value = dModel.ToString("H:mm");
                }

                string outputPath = this.outputPathOri + @"\7. Digital Simulation\Output\OP3.Simulation." + product + "." + DateTime.Now.ToString("yyyy-MM-dd HHmm") + " Countline.xlsx";
                FileInfo excelFile = new FileInfo(outputPath);
                excelPackage.SaveAs(excelFile);
                excelPackage.Dispose();


                //email.Initial(lstMailReceive, " Completed Simulation part " + product + " output", @"<p style='font-weight: bold;'>Dear [Mrs/Mr]. All,</p>
                //<p>You have received a notification about <span style='font-weight: bold; color: red; font-style: italic;'>Trial EXPORT Simulation part - output Countline </span>from PSI System. Please check at <a href='file:" + this.outputPathOri + @"\7. Digital Simulation\Output'>Export Folder</a> .</p>

                //");
            }
        }
        public async Task ExportLivePp10Minute(string product)
        {
            var lstModel = new List<PcSimulationPpLineLbp>();
            if (product == "IJ")
            {
                var lst1 = await _ctx.PcSimulationPpLineIjs.Where(x => x.Active == true).ToListAsync();
                lstModel = lst1.MapSimulation();
            }
            else if (product == "LBP")
            {
                lstModel = await _ctx.PcSimulationPpLineLbps.Where(x => x.Active == true).ToListAsync();
            }
            if (lstModel.Count < 1)
            {
                return;
            }
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (ExcelPackage excelPackage = new ExcelPackage())
            {
                ExcelWorksheet excelWorksheet;
                excelWorksheet = excelPackage.Workbook.Worksheets.Add("LivePP_block10");
                var numberRowTotal = 0;
                if (lstModel.Count < 1)
                {
                    excelPackage.Dispose();
                    return;
                }
                int currentRow = 4;
                string format = "#,###.#";
                for (int i = 0; i < lstModel.Count; i++)
                {
                    var item = lstModel[i];
                    excelWorksheet.Cells["A" + currentRow].Value = item.PalletDate;
                    excelWorksheet.Cells["B" + currentRow].Value = item.Model;
                    excelWorksheet.Cells["C" + currentRow].Value = item.Cell;
                    excelWorksheet.Cells["D" + currentRow].Value = item.ActualQuantity;
                    excelWorksheet.Cells["E" + currentRow].Value = item.Destination;
                    excelWorksheet.Cells["F" + currentRow].Value = item.Shift;
                    excelWorksheet.Cells["G" + currentRow].Value = item.PlanQuantity;
                    for (int j = 0; j < item.PpValue.Length; j++)
                    {
                        try
                        {
                            int columnCurrent = 8 + j;
                            if (item.PpValue[j] != 0)
                            {
                                excelWorksheet.Cells[currentRow, columnCurrent].Value = item.PpValue[j];
                                excelWorksheet.Cells[currentRow, columnCurrent].Style.Numberformat.Format = "#,##0.0";
                            }
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                    }
                    currentRow++;
                }
                var timeAll = lstBlockTimeSimple.OrderBy(x => x.OrderTime).Select(x => new { x.StartTime, x.EndTime }).ToList();
                var numberColumn = timeAll.Count;
                numberRowTotal += lstModel.Count;
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Font.Size = 12;
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Font.Name = "Calibri";
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                {
                    excelWorksheet.Cells["A3"].Value = "Date";
                    excelWorksheet.Cells["B3"].Value = "Model";
                    excelWorksheet.Cells["C3"].Value = "Cell";
                    excelWorksheet.Cells["D3"].Value = "Quantity";
                    excelWorksheet.Cells["E3"].Value = "Destination";
                    excelWorksheet.Cells["F3"].Value = "Shift";
                    excelWorksheet.Cells["G3"].Value = "TotalPlan";
                    for (int t = 8; t < 8 + numberColumn; t++)
                    {
                        excelWorksheet.Cells[3, t].Value = timeAll[t - 8].StartTime.ToString("HH:mm");
                        excelWorksheet.Cells[3, t].Style.TextRotation = 90;
                    }
                }
                string outputPath = this.outputPathOri + @"\7. Digital Simulation\Output\OP1.Simulation." + product + "." + DateTime.Now.ToString("yyyy-MM-dd HHmm") + " Livepp10m.xlsx";
                FileInfo excelFile = new FileInfo(outputPath);
                excelPackage.SaveAs(excelFile);
                excelPackage.Dispose();

                //email.Initial(lstMailReceive, " Completed Simulation part " + product + " output", @"<p style='font-weight: bold;'>Dear [Mrs/Mr]. All,</p>
                //<p>You have received a notification about <span style='font-weight: bold; color: red; font-style: italic;'>Trial EXPORT Simulation part - output Live PP block 10 minutes </span>from PSI System. Please check at <a href='file:" + this.outputPathOri + @"\7. Digital Simulation\Output'>Export Folder</a> .</p>

                //");
            }
        }
        public async Task ExportLivePp10MinuteWithChangepp(string product)
        {
            var lstModel = new List<PcSimulationPpLineChangeppLbp>();
            if (product == "IJ")
            {
                var lst1 = await _ctx.PcSimulationPpLineChangeppIjs.Where(x => x.Active == true).ToListAsync();
                lstModel = lst1.MapSimulationP();
            }
            else if (product == "LBP")
            {
                lstModel = await _ctx.PcSimulationPpLineChangeppLbps.Where(x => x.Active == true).ToListAsync();
            }
            if(lstModel.Count < 1)
            {
                return;
            }
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (ExcelPackage excelPackage = new ExcelPackage())
            {
                ExcelWorksheet excelWorksheet;
                excelWorksheet = excelPackage.Workbook.Worksheets.Add("LivePP_block10_ChangePP");
                var numberRowTotal = 0;
                if (lstModel.Count < 1)
                {
                    excelPackage.Dispose();
                    return;
                }
                int currentRow = 4;
                string format = "#,###.#";
                for (int i = 0; i < lstModel.Count; i++)
                {
                    var item = lstModel[i];
                    excelWorksheet.Cells["A" + currentRow].Value = item.PalletDate;
                    excelWorksheet.Cells["B" + currentRow].Value = item.Model;
                    excelWorksheet.Cells["C" + currentRow].Value = item.Cell;
                    excelWorksheet.Cells["D" + currentRow].Value = item.ActualQuantity;
                    excelWorksheet.Cells["E" + currentRow].Value = item.Destination;
                    excelWorksheet.Cells["F" + currentRow].Value = item.Shift;
                    excelWorksheet.Cells["G" + currentRow].Value = item.PlanQuantity;
                    for (int j = 0; j < item.PpValue.Length; j++)
                    {
                        try
                        {
                            int columnCurrent = 8 + j;
                            if (item.PpValue[j] != 0)
                            {
                                excelWorksheet.Cells[currentRow, columnCurrent].Value = item.PpValue[j];
                                excelWorksheet.Cells[currentRow, columnCurrent].Style.Numberformat.Format = "#,##0.0";
                            }
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                    }
                    currentRow++;
                }
                var timeAll = lstBlockTimeSimple.OrderBy(x => x.OrderTime).Select(x => new { x.StartTime, x.EndTime }).ToList();
                var numberColumn = timeAll.Count;
                numberRowTotal += lstModel.Count;
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Font.Size = 12;
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Font.Name = "Calibri";
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[3, 1, numberRowTotal + 3, 8 + numberColumn].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                {
                    excelWorksheet.Cells["A3"].Value = "Date";
                    excelWorksheet.Cells["B3"].Value = "Model";
                    excelWorksheet.Cells["C3"].Value = "Cell";
                    excelWorksheet.Cells["D3"].Value = "Quantity";
                    excelWorksheet.Cells["E3"].Value = "Destination";
                    excelWorksheet.Cells["F3"].Value = "Shift";
                    excelWorksheet.Cells["G3"].Value = "TotalPlan";
                    for (int t = 8; t < 8 + numberColumn; t++)
                    {
                        excelWorksheet.Cells[3, t].Value = timeAll[t - 8].StartTime.ToString("HH:mm");
                        excelWorksheet.Cells[3, t].Style.TextRotation = 90;
                    }
                }
                string outputPath = this.outputPathOri + @"\7. Digital Simulation\Output\OP1.Simulation." + product + "." + DateTime.Now.ToString("yyyy-MM-dd HHmm") + " Livepp10mWithChangePP.xlsx";
                FileInfo excelFile = new FileInfo(outputPath);
                excelPackage.SaveAs(excelFile);
                excelPackage.Dispose();

                //email.Initial(lstMailReceive, " Completed Simulation part " + product + " output", @"<p style='font-weight: bold;'>Dear [Mrs/Mr]. All,</p>
                //<p>You have received a notification about <span style='font-weight: bold; color: red; font-style: italic;'>Trial EXPORT Simulation part - output Live PP block 10 minutes </span>from PSI System. Please check at <a href='file:" + this.outputPathOri + @"\7. Digital Simulation\Output'>Export Folder</a> .</p>

                //");
            }
        }
        public async Task ExportParallel(string product)
        {
            int a = 5;
            List<VStructureIjSimulationParallel> lstModel = new List<VStructureIjSimulationParallel>();
            if(product == "IJ")
            {
                lstModel = await _ctx.VStructureIjSimulationParallels.ToListAsync();
            }
            else if (product == "LBP")
            {
                var lstModel1 = await _ctx.VStructureLbpSimulationParallels.ToListAsync();
                lstModel = lstModel1.MapSimulation();
            }
            if(lstModel.Count < 1)
            {
                return;
            }
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (ExcelPackage excelPackage = new ExcelPackage())
            {
                ExcelWorksheet excelWorksheet;
                excelWorksheet = excelPackage.Workbook.Worksheets.Add("Parallel");
                var numberRowTotal = 0;
                int currentRow = 7;
                string format = "#,###.#";
                for (int i = 0; i < lstModel.Count; i++)
                {
                    var item = lstModel[i];
                    excelWorksheet.Cells["A" + currentRow].Value = (i + 1);
                    excelWorksheet.Cells["B" + currentRow].Value = item.PartNo;
                    excelWorksheet.Cells["C" + currentRow].Value = "";
                    excelWorksheet.Cells["D" + currentRow].Value = item.PartName;
                    excelWorksheet.Cells["E" + currentRow].Value = item.PartnoVendor;
                    excelWorksheet.Cells["F" + currentRow].Value = item.Vendor;
                    excelWorksheet.Cells["G" + currentRow].Value = item.Vendor;
                    excelWorksheet.Cells["H" + currentRow].Value = item.Ratio;
                    excelWorksheet.Cells["I" + currentRow].Value = item.Pair;
                    excelWorksheet.Cells["J" + currentRow].Value = item.Master;
                    currentRow++;
                }
                numberRowTotal += lstModel.Count;
                excelWorksheet.Cells[6, 1, numberRowTotal + 6, 10].Style.Font.Size = 12;
                excelWorksheet.Cells[6, 1, numberRowTotal + 6, 10].Style.Font.Name = "Calibri";
                excelWorksheet.Cells[6, 1, numberRowTotal + 6, 10].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[6, 1, numberRowTotal + 6, 10].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[6, 1, numberRowTotal + 6, 10].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[6, 1, numberRowTotal + 6, 10].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                {
                    excelWorksheet.Cells["A6"].Value = "No";
                    excelWorksheet.Cells["B6"].Value = "Part No";
                    excelWorksheet.Cells["C6"].Value = "Part & Alt";
                    excelWorksheet.Cells["D6"].Value = "Part Name";
                    excelWorksheet.Cells["E6"].Value = "PartNo & Vendor";
                    excelWorksheet.Cells["F6"].Value = "Vendor";
                    excelWorksheet.Cells["G6"].Value = "Vendor Code";
                    excelWorksheet.Cells["H6"].Value = "Ratio";
                    excelWorksheet.Cells["I6"].Value = "Set";
                    excelWorksheet.Cells["J6"].Value = "Master";
                }

                ExcelWorksheet excelWorksheet2 = excelPackage.Workbook.Worksheets.Add("LOG order List");
                currentRow = 2;
                for (int i = 0; i < lstModel.Count; i++)
                {
                    var item = lstModel[i];
                    excelWorksheet2.Cells["A" + currentRow].Value = item.PartNo;
                    excelWorksheet2.Cells["B" + currentRow].Value = item.PartName;
                    excelWorksheet2.Cells["C" + currentRow].Value = item.Vendor;
                    currentRow++;
                }
                excelWorksheet2.Cells[1, 1, numberRowTotal + 1, 3].Style.Font.Size = 12;
                excelWorksheet2.Cells[1, 1, numberRowTotal + 1, 3].Style.Font.Name = "Calibri";
                excelWorksheet2.Cells[1, 1, numberRowTotal + 1, 3].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                excelWorksheet2.Cells[1, 1, numberRowTotal + 1, 3].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                excelWorksheet2.Cells[1, 1, numberRowTotal + 1, 3].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                excelWorksheet2.Cells[1, 1, numberRowTotal + 1, 3].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                {
                    excelWorksheet2.Cells["A1"].Value = "Part No";
                    excelWorksheet2.Cells["B1"].Value = "Part Name";
                    excelWorksheet2.Cells["C1"].Value = "BC";
                }
                string outputPath = this.outputPathOri + @"\7. Digital Simulation\Output\4.Simulation." + product + "." + DateTime.Now.ToString("yyyy-MM-dd HHmm") + " Parallel.xlsx";
                FileInfo excelFile = new FileInfo(outputPath);
                excelPackage.SaveAs(excelFile);
                excelPackage.Dispose();

                //email.Initial(lstMailReceive, " Completed Simulation part " + product + " output", @"<p style='font-weight: bold;'>Dear [Mrs/Mr]. All,</p>
                //<p>You have received a notification about <span style='font-weight: bold; color: red; font-style: italic;'>Trial EXPORT Simulation part - output Live PP block 10 minutes </span>from PSI System. Please check at <a href='file:" + this.outputPathOri + @"\7. Digital Simulation\Output'>Export Folder</a> .</p>

                //");
            }
        }
        public async Task ExportLeadtime(string product)
        {
            var lstDemandByCell = await _ctx.PcSimulationLeadtimeLbps.AsNoTracking().Where(x => x.Active == true).Take(200).ToListAsync();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (ExcelPackage excelPackage = new ExcelPackage())
            {
                ExcelWorksheet excelWorksheet;
                excelWorksheet = excelPackage.Workbook.Worksheets.Add("Leadtime by Cell");
                var numberRowTotal = lstDemandByCell.Count;
                if (numberRowTotal < 1)
                {
                    excelPackage.Dispose();
                    return;
                }
                int currentRow = 8;
                var numberColumn2 = lstBlockTimeAll.Count();
                int len = 0;
                for (int i = 0; i < numberRowTotal; i++)
                {
                    var strucItem = lstDemandByCell[i];
                    if (strucItem.TotalLt.All(x => x == 0))
                    {
                        continue;
                    }
                    excelWorksheet.Cells["A" + currentRow].Value = currentRow;
                    excelWorksheet.Cells["B" + currentRow].Value = strucItem.PartNo;
                    excelWorksheet.Cells["C" + currentRow].Value = strucItem.Cell;
                    excelWorksheet.Cells["D" + currentRow].Value = strucItem.Model;
                    excelWorksheet.Cells["E" + currentRow].Value = "Demand";
                    excelWorksheet.Cells["E" + (currentRow + 1)].Value = "Has Plan";
                    excelWorksheet.Cells["B" + (currentRow + 1)].Value = "LeadtimeBlock";
                    excelWorksheet.Cells["C" + (currentRow + 1)].Value = strucItem.Leadtime;
                    excelWorksheet.Cells["E" + (currentRow + 2)].Value = "Leadtime";
                    excelWorksheet.Cells["E" + (currentRow + 3)].Value = "QtyKeepCell";
                    excelWorksheet.Cells["E" + (currentRow + 4)].Value = "TotalLeadTime";
                    excelWorksheet.Cells["B" + (currentRow + 2)].Value = "QtyKeepCellBlock";
                    excelWorksheet.Cells["C" + (currentRow + 2)].Value = strucItem.QtyKeepCell;
                    //var arrActive = strucItem.TotalLeadTimeArr.Select((value, index) => new { value, index })
                    //  .Where(item => item.value != 0)
                    //  .Select(item => item.index)
                    //  .ToArray();
                    //for (int j = 0; j < arrActive.Length; j++)
                    //{
                    //    //if (strucItem.TotalLeadTimeArr[arrActive[j]] != 0)
                    //    //{
                    //    excelWorksheet.Cells[currentRow, arrActive[j] + 6].Value = strucItem.DemandArr[arrActive[j]];
                    //    excelWorksheet.Cells[currentRow + 1, arrActive[j] + 6].Value = strucItem.HasPlanArr[arrActive[j]];
                    //    excelWorksheet.Cells[currentRow + 2, arrActive[j] + 6].Value = strucItem.LeadtimeArr[arrActive[j]];
                    //    excelWorksheet.Cells[currentRow + 3, arrActive[j] + 6].Value = strucItem.QtyKeepCellArr[arrActive[j]];
                    //    excelWorksheet.Cells[currentRow + 4, arrActive[j] + 6].Value = strucItem.TotalLeadTimeArr[arrActive[j]];
                    //    excelWorksheet.Cells[currentRow, arrActive[j] + 6, currentRow + 4, arrActive[j] + 6].Style.Numberformat.Format = "#,##0.0";

                    //    excelWorksheet.Cells[currentRow + 4, arrActive[j] + 6].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
                    //    //}
                    //}
                    len = strucItem.TotalLt.Length;
                    for (int j = 0; j < len; j++)
                    {
                        excelWorksheet.Cells[currentRow, j + 6].Value = strucItem.DemandArr[j];
                        excelWorksheet.Cells[currentRow + 1, j + 6].Value = strucItem.HasPlan[j];
                        excelWorksheet.Cells[currentRow + 2, j + 6].Value = strucItem.LtValue[j];
                        excelWorksheet.Cells[currentRow + 3, j + 6].Value = strucItem.QtyKeepArr[j];
                        excelWorksheet.Cells[currentRow + 4, j + 6].Value = strucItem.TotalLt[j];
                    }

                    excelWorksheet.Cells[currentRow + 4, 1, currentRow + 4, len + 5].Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
                    currentRow += 5;
                }
                //for (int i = 0; i < numberRowTotal; i++)
                //{
                //    var strucItem = lstDemandByCell[i];
                //    if (strucItem.TotalLeadTimeArr.All(x => x == 0))
                //    {
                //        continue;
                //    }
                //    excelWorksheet.Cells["A" + currentRow].Value = currentRow;
                //    excelWorksheet.Cells["B" + currentRow].Value = strucItem.PartNo;
                //    excelWorksheet.Cells["C" + currentRow].Value = strucItem.Cell;
                //    excelWorksheet.Cells["D" + currentRow].Value = strucItem.Model;
                //    excelWorksheet.Cells["E" + currentRow].Value = "TotalLeadTime";
                //    len = strucItem.TotalLeadTimeArr.Length;
                //    var arrActive = strucItem.TotalLeadTimeArr.Select((value, index) => new { value, index })
                //          .Where(item => item.value != 0)
                //          .Select(item => item.index)
                //          .ToArray();
                //    for (int j = 0; j < arrActive.Length; j++)
                //    {
                //        excelWorksheet.Cells[currentRow, arrActive[j] + 6].Value = strucItem.TotalLeadTimeArr[arrActive[j]];
                //    }
                //    currentRow++;
                //}
                excelWorksheet.Cells[8, 6, currentRow, len + 5].Style.Numberformat.Format = "#,##0.0";
                excelWorksheet.Cells[5, 1, currentRow * 5 + 7, 5 + numberColumn2].Style.Font.Size = 12;
                excelWorksheet.Cells[5, 1, currentRow * 5 + 7, 5 + numberColumn2].Style.Font.Name = "Calibri";
                excelWorksheet.Cells["A6"].Value = "STT";
                excelWorksheet.Cells["B6"].Value = "Part No";
                excelWorksheet.Cells["C6"].Value = "Cell";
                excelWorksheet.Cells["D6"].Value = "Model";
                excelWorksheet.Cells["E6"].Value = "Type";
                excelWorksheet.Cells[5, 5, 7, 5 + numberColumn2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                excelWorksheet.Cells[5, 5, 7, 5 + numberColumn2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LimeGreen);
                excelWorksheet.Cells[7, 5, 7, 5 + numberColumn2].Style.TextRotation = 90;
                for (int t = 6; t < 6 + numberColumn2; t++)
                {
                    excelWorksheet.Cells[6, t].Value = lstBlockTimeAll[t - 6].ToString("d-MMM");
                    excelWorksheet.Cells[7, t].Value = lstBlockTimeAll[t - 6].ToString("HH:mm");
                }

                string outputPath = this.outputPathOri + @"\7. Digital Simulation\Output\OP4.SimulationLBP." + DateTime.Now.ToString("yyyy-MM-dd HHmm") + ".LeadTimeByCell.xlsx";
                FileInfo excelFile = new FileInfo(outputPath);
                excelPackage.SaveAs(excelFile);
                excelPackage.Dispose();
                Console.WriteLine($"Da xuat file: {DateTime.Now.ToString("HH:mm:ss.fff")}");
            }
        
        }
        public async Task PushLivePp10Minute(string product)
        {
            ADO.doChange("delete FROM public.pc_simulation_pp_line_" + product.ToLower() + ";");
            var lstGroup = new List<VGroupMvLivePpIjBlock10>();//await _ctx.VGroupMvLivePpIjBlock10s.ToListAsync();
            if (product == "IJ")
            {
                lstGroup = await _ctx.VGroupMvLivePpIjBlock10s.AsNoTracking().ToListAsync();
            }
            else if (product == "LBP")
            {
                lstGroup = await _ctx.VGroupMvLivePpLbpBlock10s.AsNoTracking().Select(x => new VGroupMvLivePpIjBlock10() { ActualQty = x.ActualQty, BKey = x.BKey, PlanQty = x.PlanQty }).ToListAsync();
            }
            var lstDetail = new List<MvLivePpIjBlock10>();//await _ctx.MvLivePpIjBlock10s.ToListAsync();
            if (product == "IJ")
            {
                lstDetail = await _ctx.MvLivePpIjBlock10s.AsNoTracking().ToListAsync();
            }
            else if (product == "LBP")
            {
                lstDetail = await _ctx.MvLivePpLbpBlock10s.AsNoTracking().Select(x => new MvLivePpIjBlock10() { BKey = x.BKey, BActual = x.BActual, BQuantity = x.BQuantity, BTime = x.BTime }).ToListAsync();
            }
            ConcurrentDictionary<(string, string), (double, double)> dictQty = new ConcurrentDictionary<(string, string), (double, double)>();
            foreach (var dtail in lstDetail)
            {
                dictQty.AddOrUpdate((dtail.BKey ?? "", dtail.BTime ?? ""), (dtail.BActual.Value, dtail.BQuantity.Value), (key, oldValue) => (dtail.BActual.Value, dtail.BQuantity.Value));
            }
            var timeAll = lstBlockTimeSimple.OrderBy(x => x.OrderTime).Select(x => new { x.StartTime, x.EndTime }).ToList();
            var timeDay = lstBlockTimeSimple.Where(x => x.Shift == "D").OrderBy(x => x.OrderTime).Select(x => new { x.StartTime, x.EndTime }).ToList();
            var timeNight = lstBlockTimeSimple.Where(x => x.Shift == "N").OrderBy(x => x.OrderTime).Select(x => new { x.StartTime, x.EndTime }).ToList();
            ConcurrentDictionary<(TimeOnly, TimeOnly), (string, string, int)> dicPositionTime = new ConcurrentDictionary<(TimeOnly, TimeOnly), (string, string, int)>();
            
            var timeStartNewDay = new TimeOnly(23, 50);
            int indexStartNewDay = Array.FindIndex(lstBlockTimeSimple, x => x.StartTime == timeStartNewDay);

            for (int i = 0; i < timeAll.Count; i++)
            {
                var dtail = timeAll[i];
                dicPositionTime.AddOrUpdate((dtail.StartTime, dtail.EndTime), (dtail.StartTime.ToString("HH:mm"), dtail.EndTime.ToString("HH:mm"), i), (key, oldValue) => (dtail.StartTime.ToString("HH:mm"), dtail.EndTime.ToString("HH:mm"), i));
            }
            var lstAdd = new List<PcSimulationPpLineIj>();
            var lengthValue = timeAll.Count;
            CultureInfo provider = CultureInfo.InvariantCulture;
            lstGroup.AsParallel().ForAll(g =>
            {
                var keys = g.BKey.Split('_');
                var itemAdd = new PcSimulationPpLineIj()
                {
                    PalletDate = keys[0],
                    Model = keys[1],
                    Cell = keys[2],
                    ActualQuantity = g.PlanQty.Value,//g.ActualQty.Value,
                    Destination = keys[3],
                    Shift = keys[4],
                    PlanQuantity = 0//g.PlanQty.Value
                };
                var arrValue = new double[lengthValue];
                DateTime dtDayShift = DateTime.ParseExact(keys[0], "dd-MM-yyyy", provider);
                string sDtDayShift = dtDayShift.ToString("yyyy-MM-dd");
                if (keys[4] == "D")
                {
                    for (int j = 0; j < timeDay.Count; j++)
                    {
                        var valKey = dicPositionTime[(timeDay[j].StartTime, timeDay[j].EndTime)];
                        try
                        {
                            arrValue[valKey.Item3] = dictQty[(g.BKey, sDtDayShift + " " + valKey.Item1 + "_" + sDtDayShift + " " + valKey.Item2)].Item2;
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                    }
                }
                else if (keys[4] == "N")
                {
                    DateTime dtNightShift = dtDayShift.AddDays(1);
                    string sDtNightShift = dtNightShift.ToString("yyyy-MM-dd");
                    for (int j = 0; j < timeNight.Count; j++)
                    {
                        var valKey = dicPositionTime[(timeNight[j].StartTime, timeNight[j].EndTime)];
                        try
                        {
                            if (valKey.Item3 < indexStartNewDay)
                            {
                                arrValue[valKey.Item3] = dictQty[(g.BKey, sDtDayShift + " " + valKey.Item1 + "_" + sDtDayShift + " " + valKey.Item2)].Item2;
                            }
                            else if (valKey.Item3 == indexStartNewDay)
                            {
                                arrValue[valKey.Item3] = dictQty[(g.BKey, sDtDayShift + " " + valKey.Item1 + "_" + sDtNightShift + " " + valKey.Item2)].Item2;
                            }
                            else if (valKey.Item3 > indexStartNewDay)
                            {
                                arrValue[valKey.Item3] = dictQty[(g.BKey, sDtNightShift + " " + valKey.Item1 + "_" + sDtNightShift + " " + valKey.Item2)].Item2;
                            }
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                }
                itemAdd.PpValue = arrValue;
                lock (lockObj)
                {
                    lstAdd.Add(itemAdd);
                }
            });
            if (product == "IJ")
            {
                _ctx.PcSimulationPpLineIjs.AddRange(lstAdd);
                await _ctx.SaveChangesAsync();
            }
            else if (product == "LBP")
            {
                var lstAdd1 = lstAdd.MapSimulation();
                _ctx.PcSimulationPpLineLbps.AddRange(lstAdd1);
                await _ctx.SaveChangesAsync();
            }
        }
        public async Task PushLivePp10MinuteWithChangepp(string product)
        {
            ADO.doChange("delete FROM public.pc_simulation_pp_line_changepp_" + product.ToLower() + ";");
            var modelBase = new List<VSimulationChangeppJoinLbp>();
            if(product == "LBP")
            {
                modelBase = await _ctx.VSimulationChangeppJoinLbps.Where(x => x.FromDate != null && x.ToDate != null).ToListAsync();
            }
            else if(product == "IJ")
            {
                var md = await _ctx.VSimulationChangeppJoinIjs.Where(x => x.FromDate != null && x.ToDate != null).ToListAsync();
                modelBase = md.MapSimulationP();
            }
            if(modelBase.Count < 1)
            {
                return;
            }
            var modelAdd = new List<PcSimulationPpLineChangeppLbp>();
            var timeAll = lstBlockTimeSimple.OrderBy(x => x.OrderTime).Select(x => new { x.StartTime, x.EndTime }).ToList();
            ConcurrentDictionary<TimeOnly, int> dicPositionTime = new ConcurrentDictionary<TimeOnly, int>();
            for (int i = 0; i < timeAll.Count; i++)
            {
                var dtail = timeAll[i];
                dicPositionTime.AddOrUpdate(dtail.StartTime, i, (key, oldValue) => i);
            }
            for (int i = 0; i < modelBase.Count; i++)
            {
                try
                {
                    var item = modelBase[i];
                    var trend = item.AdjQty.HasValue && item.AdjQty.Value > 0 ? 1 : -1;
                    var timeFrom = TimeOnly.FromDateTime(item.FromDate.Value);
                    var timeTo = TimeOnly.FromDateTime(item.ToDate.Value);
                    var updateFromIndex = dicPositionTime[timeFrom];
                    var updateToIndex = dicPositionTime[timeTo];
                    var itemAdd = item.MapSimulationP();
                    itemAdd.PpValue = new double[item.PpValue.Length];
                    for(int j = updateFromIndex; j <= updateToIndex; j++)
                    {
                        itemAdd.PpValue[j] = item.PpValue[j] * trend;
                    }
                    modelAdd.Add(itemAdd);
                }
                catch (Exception)
                {
                    continue;
                }
            }
            if (product == "LBP")
            {
                _ctx.PcSimulationPpLineChangeppLbps.AddRange(modelAdd);
                await _ctx.SaveChangesAsync();
            }
            else if (product == "IJ")
            {
                var modelAdd2 = modelAdd.MapSimulationP();
                _ctx.PcSimulationPpLineChangeppIjs.AddRange(modelAdd2);
                await _ctx.SaveChangesAsync();
            }
        }
        public async Task PushCountlineByStructure(string product)
        {
            ADO.doChange("delete FROM public.pc_simulation_countline_" + product.ToLower() + ";");
            var lstStructure = new List<VStructureIjSimulation>();
            if (product == "IJ")
            {
                lstStructure = await _ctx.VStructureIjSimulations.AsNoTracking().ToListAsync();
            }
            else if (product == "LBP")
            {
                var lstStructureTemp = await _ctx.VStructureLbpSimulations.AsNoTracking().ToListAsync();
                lstStructure = lstStructureTemp.MapSimulation();
            }
            var header = await _ctx.TodStructureOutputHeaders.Where(x => x.Active == true && x.Product == product)
                .Select(x => new { Model = x.Model, Merchandise = x.Merchandise, MerCode = x.MerCode })
                .AsNoTracking()
                .FirstAsync();
            
            var lstDetail = new List<VGroupMvLivePpIjBlock10CountLine>();
            if (product == "IJ")
            {
                lstDetail = await _ctx.VGroupMvLivePpIjBlock10CountLines.AsNoTracking().ToListAsync();
            }
            else if (product == "LBP")
            {
                var lstDetailTemp = await _ctx.VGroupMvLivePpLbpBlock10CountLines.AsNoTracking().ToListAsync();
                lstDetail = lstDetailTemp.MapSimulation();
            }
            ConcurrentDictionary<string, int> dicPositionMerchandise = new ConcurrentDictionary<string, int>();
            foreach (var h in header.MerCode)
            {
                var arh = h.Split(':');
                dicPositionMerchandise.AddOrUpdate(arh[1], arh[0].ObjToIntAble(), (key, val) => arh[0].ObjToIntAble());
            }

            var lstResult = new List<PcSimulationCountlineIj>();
            var lstStructure1 = Partitioner.Create(lstStructure);
            var cColumn = lstBlockTimeAll.Length;
            var rangTime = lstBlockTimeAll.Select(x => x.ToString("yyyy-MM-dd HH:mm") + "_" + x.AddMinutes(10).ToString("yyyy-MM-dd HH:mm")).ToArray();
            lstStructure1.AsParallel().ForAll(structItem =>
            {
                var itemAdd = new PcSimulationCountlineIj()
                {
                    DayQty = 12,
                    PartNo = structItem.PartNo ?? "",
                    Vendor = structItem.Vendor ?? "",
                    Ratio = structItem.Ratio ?? "",
                    Model = "",
                    Merchandise = "",
                    Bc = structItem.Bc,
                    Dim = structItem.Dim,
                    DoPic = structItem.DoPic,
                    EffectiveDateChange = structItem.EffectiveDateChange,
                    Factory = structItem.Factory,
                    PartName = structItem.PartName,
                    PoPic = structItem.PoPic,
                    RatioChange = structItem.RatioChange,
                    DateVal = lstBlockTimeAll
                };
                if (structItem.Model != null && structItem.Model.Length > 0)
                {
                    itemAdd.Model = string.Join('+', structItem.Model.Distinct());
                }
                var val = new double[cColumn];
                if (structItem.Merchandise != null && structItem.Merchandise.Length > 0)
                {
                    itemAdd.Merchandise = string.Join('+', structItem.Merchandise.Distinct());
                    var lstMer = structItem.Merchandise.Distinct().ToList();
                    if (lstMer.Count > 0)
                    {
                        try
                        {
                            var finMer = lstDetail.Where(x => lstMer.Contains(x.Merchandise)).ToList();
                            var lstHeaderIndex = Array.FindIndex(header.MerCode, x => lstMer.Contains(x));
                            for (int j = 0; j < cColumn; j++)
                            {
                                var dModel = rangTime[j];
                                val[j] = finMer.Where(x => x.BTime == dModel).Sum(x => x.CountLine ?? 0);
                            }
                        }
                        catch (Exception)
                        {

                        }

                    }

                }
                itemAdd.CountValue = val;
                lock (lockObj)
                {
                    lstResult.Add(itemAdd);
                }
            });
            if (product == "IJ")
            {
                _ctx.PcSimulationCountlineIjs.AddRange(lstResult);
                await _ctx.SaveChangesAsync();
            }
            else if (product == "LBP")
            {
                var lstResult1 = lstResult.MapSimulation();
                _ctx.PcSimulationCountlineLbps.AddRange(lstResult1);
                await _ctx.SaveChangesAsync();
            }
        }
        public async Task PushPartMaster(string product)
        {
            ADO.doChange("delete FROM public.pc_simulation_part_master_" + product.ToLower() + ";");
            var lstStructure = new List<VStructureIjSimulation>();//await _ctx.VStructureIjSimulations.AsNoTracking().ToListAsync();
            if (product == "IJ")
            {
                lstStructure = await _ctx.VStructureIjSimulations.AsNoTracking().ToListAsync();
            }
            else if (product == "LBP")
            {
                var lstStructureTemp = await _ctx.VStructureLbpSimulations.AsNoTracking().ToListAsync();
                lstStructure = lstStructureTemp.MapSimulation();
            }
            var header = await _ctx.TodStructureOutputHeaders.Where(x => x.Active == true && x.Product == product)
                .Select(x => new { Model = x.Model, Merchandise = x.Merchandise, MerCode = x.MerCode })
                .AsNoTracking()
                .FirstAsync();
            var lstDetail = new List<MvLivePpIjBlock10PartMaster>();// await _ctx.MvLivePpIjBlock10PartMasters.AsNoTracking().ToListAsync();
            if (product == "IJ")
            {
                lstDetail = await _ctx.MvLivePpIjBlock10PartMasters.AsNoTracking().ToListAsync();
            }
            else if (product == "LBP")
            {
                var lstDetailTemp = await _ctx.MvLivePpLbpBlock10PartMasters.AsNoTracking().ToListAsync();
                lstDetail = lstDetailTemp.MapSimulation();
            }
            ConcurrentDictionary<(string, string, string), (double, double)> dictQty = new ConcurrentDictionary<(string, string, string), (double, double)>();
            ConcurrentDictionary<string, int> dicPositionMerchandise = new ConcurrentDictionary<string, int>();
            foreach (var h in header.MerCode)
            {
                var arh = h.Split(':');
                dicPositionMerchandise.AddOrUpdate(arh[1], arh[0].ObjToIntAble(), (key, val) => arh[0].ObjToIntAble());
            }
            foreach (var dtail in lstDetail)
            {
                dictQty.AddOrUpdate((dtail.PalletDate ?? "", dtail.Merchandise ?? "", dtail.BTime ?? ""), (dtail.BActual.Value, dtail.BQuantity.Value), (key, oldValue) => (oldValue.Item1 + dtail.BActual.Value, oldValue.Item2 + dtail.BQuantity.Value));
            }
            var lstResult = new List<PcSimulationPartMasterLbp>();
            var lstStructure1 = Partitioner.Create(lstStructure);
            var cTotalColumn = lstBlockTimeAll.Length;
            var lstScanDate = new List<SimualationPartmasterTimeView>();
            foreach(var s in lstBlockTimeAll)
            {
                lstScanDate.Add(new SimualationPartmasterTimeView() { TimeF = s });
            }
            lstStructure1.AsParallel().ForAll(structItem =>
            {
                if (structItem != null)
                {
                    var itemAdd = new PcSimulationPartMasterLbp()
                    {
                        Dim = structItem.Dim ?? "",
                        DoPic = structItem.DoPic ?? "",
                        EffectivedateChange = structItem.EffectiveDateChange ?? "",
                        Factory = structItem.Factory ?? "",
                        PartName = structItem.PartName ?? "",
                        PartNo = structItem.PartNo ?? "",
                        PartnoBc = structItem.PartnoBc ?? "",
                        PoPic = structItem.PoPic ?? "",
                        Pr = structItem.Pr ?? "",
                        Ratio = structItem.Ratio ?? "",
                        RatioChange = structItem.RatioChange ?? "",
                        Unit = structItem.Unit ?? "",
                        Vendor = structItem.Vendor ?? "",
                        Pair = structItem.Pair,
                        Model = "",
                        Merchandise = "",
                        Destination = "",
                        DateVal = lstBlockTimeAll
                    };
                    if (structItem.Model != null && structItem.Model.Length > 0)
                    {
                        itemAdd.Model = string.Join('+', structItem.Model.Distinct());
                    }
                    if (structItem.Destination != null && structItem.Destination.Length > 0)
                    {
                        itemAdd.Destination = string.Join('+', structItem.Destination.Distinct());
                    }
                    var valPp = new double[cTotalColumn];
                    if (structItem.Merchandise != null && structItem.Merchandise.Length > 0)
                    {
                        itemAdd.Merchandise = string.Join('+', structItem.Merchandise.Distinct());
                        var dRatio = structItem.Ratio.ObjToDoubleAble();
                        for (int j = 0; j < cTotalColumn; j++)
                        {
                            var scanDate = lstScanDate[j];
                            double valueAt = 0;
                            foreach (var m in structItem.Merchandise.Distinct())
                            {
                                //if (j == 0) Console.WriteLine($"Mer {m}");
                                if (dictQty.ContainsKey((scanDate.PalletDate, m, scanDate.BTime)))
                                {
                                    try
                                    {
                                        valueAt += dictQty[(scanDate.PalletDate, m, scanDate.BTime)].Item2 * structItem.Usage[dicPositionMerchandise[m]].ObjToDoubleAble();
                                        //if(scanDate.BTime.Contains("2024-05-03 08:00_"))
                                        //{
                                        //    Console.WriteLine($"J {j} PalletDate {scanDate.PalletDate} Time {scanDate.BTime} Mer {m} Usage {structItem.Usage[dicPositionMerchandise[m]]} Quantity {dictQty[(scanDate.PalletDate, m, scanDate.BTime)].Item2 * structItem.Usage[dicPositionMerchandise[m]].ObjToDoubleAble()}");
                                        //}
                                    }
                                    catch (Exception)
                                    {
                                        continue;
                                    }
                                }
                            }
                            //if(j == 0/* && scanDate.BTime.Contains("2024-05-03 08:00_")*/) Console.WriteLine($"Ratio {dRatio}");
                            if (structItem.EffectiveDateChange != null && structItem.EffectiveDateChange.Length == 8 && structItem.RatioChange != null && structItem.RatioChange != "")
                            {
                                if (Convert.ToInt64(scanDate.StrDate) >= Convert.ToInt64(structItem.EffectiveDateChange))
                                {
                                    dRatio = structItem.RatioChange.ObjToDoubleAble();
                                    //if (j == 0/* && scanDate.BTime.Contains("2024-05-03 08:00_")*/) Console.WriteLine($"Ratio change {dRatio}");
                                }
                            }
                            valPp[j] = valueAt * dRatio / 100;
                        }
                    }
                    itemAdd.PpValue = valPp;
                    itemAdd.Usage = structItem.Usage.Select(s => double.TryParse(s, out double result) ? result : 0.0).ToArray();
                    lock (lockObj)
                    {
                        lstResult.Add(itemAdd);
                    }
                }
            });
            _ctx.PcSimulationPartMasterLbps.AddRange(lstResult);
            await _ctx.SaveChangesAsync();
            //if (product == "IJ")
            //{
            //    _ctx.PcSimulationPartMasterIjs.AddRange(lstResult);
            //    await _ctx.SaveChangesAsync();
            //}
            //else if (product == "LBP")
            //{
            //    var lstResult1 = lstResult.MapSimulation();
            //    _ctx.PcSimulationPartMasterLbps.AddRange(lstResult1);
            //    await _ctx.SaveChangesAsync();
            //}
            //email.Initial(lstMailReceive, " Completed Simulation part " + product + " output", @"<p style='font-weight: bold;'>Dear [Mrs/Mr]. All,</p>
            //    <p>You have received a notification about <span style='font-weight: bold; color: red; font-style: italic;'>Trial Run Simulation part - output Part master </span>from PSI System.</p>

            //    ");
        }
        public async Task PushPartMasterChangePP(string product)
        {
            ADO.doChange("delete FROM public.pc_simulation_part_master_changepp_" + product.ToLower() + ";");
            var structureChangePP = new List<VStructureLbpSimulationChangepp>();
            if (product == "IJ")
            {
                var lstTemp = await _ctx.VStructureIjSimulationChangepps.ToListAsync();
                structureChangePP = lstTemp.MapSimulationP();
            }
            else if (product == "LBP")
            {
                structureChangePP = await _ctx.VStructureLbpSimulationChangepps.ToListAsync();
            }
            var header = await _ctx.TodStructureOutputHeaders.Where(x => x.Active == true && x.Product == product)
                .Select(x => new { Model = x.Model, Merchandise = x.Merchandise, MerCode = x.MerCode })
                .AsNoTracking()
                .FirstAsync();
            var liveppchange = new List<VLivePpLbpBlock10PartMasterChangepp>();
            if (product == "LBP")
            {
                liveppchange = await _ctx.VLivePpLbpBlock10PartMasterChangepps.ToListAsync();
            }
            else if (product == "IJ")
            {
                var lstTemp2 = await _ctx.VLivePpIjBlock10PartMasterChangepps.ToListAsync();
                liveppchange = lstTemp2.MapSimulationP();
            }
            ConcurrentDictionary<(string, string, string), (double, double)> dictQty = new ConcurrentDictionary<(string, string, string), (double, double)>();
            ConcurrentDictionary<string, int> dicPositionMerchandise = new ConcurrentDictionary<string, int>();
            ConcurrentDictionary<string, int> dicPositionTime = new ConcurrentDictionary<string, int>();
            for(int i = 0; i < lstBlockTimeSimple.Length; i++)
            {
                var md = lstBlockTimeSimple[i];
                dicPositionTime.AddOrUpdate(md.StartTime.ToString("HH:mm"), i, (key, val) => i);
            }
            foreach (var h in header.MerCode)
            {
                var arh = h.Split(':');
                dicPositionMerchandise.AddOrUpdate(arh[1], arh[0].ObjToIntAble(), (key, val) => arh[0].ObjToIntAble());
            }
            foreach (var dtail in liveppchange)
            {
                var subTime = dtail.BTime.Substring(11, 5);
                var idx = dicPositionTime[subTime];
                if (dtail.PpValue[idx] != 0)
                {
                    dictQty.AddOrUpdate((dtail.PalletDate ?? "", dtail.Merchandise ?? "", dtail.BTime ?? ""), (dtail.BActual.Value, dtail.BQuantity.Value), (key, oldValue) => (dtail.BActual.Value, dtail.BQuantity.Value));
                }
            }
            var lstResult = new List<PcSimulationPartMasterChangeppIj>();
            var lstStructure1 = Partitioner.Create(structureChangePP);
            var cTotalColumn = lstBlockTimeAll.Length;
            var lstScanDate = new List<SimualationPartmasterTimeView>();
            foreach (var s in lstBlockTimeAll)
            {
                lstScanDate.Add(new SimualationPartmasterTimeView() { TimeF = s });
            }
            lstStructure1.AsParallel().ForAll(structItem =>
            {
                if (structItem != null)
                {
                    var itemAdd = new PcSimulationPartMasterChangeppIj()
                    {
                        Dim = structItem.Dim ?? "",
                        DoPic = structItem.DoPic ?? "",
                        EffectivedateChange = structItem.EffectiveDateChange ?? "",
                        Factory = structItem.Factory ?? "",
                        PartName = structItem.PartName ?? "",
                        PartNo = structItem.PartNo ?? "",
                        PartnoBc = structItem.PartnoBc ?? "",
                        PoPic = structItem.PoPic ?? "",
                        Pr = structItem.Pr ?? "",
                        Ratio = structItem.Ratio ?? "",
                        RatioChange = structItem.RatioChange ?? "",
                        Unit = structItem.Unit ?? "",
                        Vendor = structItem.Vendor ?? "",
                        Pair = structItem.Pair,
                        Model = "",
                        Merchandise = "",
                        Destination = ""
                    };
                    if (structItem.Model != null && structItem.Model.Length > 0)
                    {
                        itemAdd.Model = string.Join('+', structItem.Model.Distinct());
                    }
                    if (structItem.Destination != null && structItem.Destination.Length > 0)
                    {
                        itemAdd.Destination = string.Join('+', structItem.Destination.Distinct());
                    }
                    var valPp = new double[cTotalColumn];
                    if (structItem.Merchandise != null && structItem.Merchandise.Length > 0)
                    {
                        itemAdd.Merchandise = string.Join('+', structItem.Merchandise.Distinct());
                        var dRatio = structItem.Ratio.ObjToDoubleAble();
                        for (int j = 0; j < cTotalColumn; j++)
                        {
                            var scanDate = lstScanDate[j];
                            double valueAt = 0;
                            foreach (var m in structItem.Merchandise.Distinct())
                            {
                                if (dictQty.ContainsKey((scanDate.PalletDate, m, scanDate.BTime)))
                                {
                                    try
                                    {
                                        valueAt += dictQty[(scanDate.PalletDate, m, scanDate.BTime)].Item2 * structItem.Usage[dicPositionMerchandise[m]].ObjToDoubleAble();
                                    }
                                    catch (Exception)
                                    {
                                        continue;
                                    }
                                }
                            }
                            if (structItem.EffectiveDateChange != null && structItem.EffectiveDateChange.Length == 8 && structItem.RatioChange != null && structItem.RatioChange != "")
                            {
                                if (Convert.ToInt64(scanDate.StrDate) >= Convert.ToInt64(structItem.EffectiveDateChange))
                                {
                                    dRatio = structItem.RatioChange.ObjToDoubleAble();
                                }
                            }
                            valPp[j] = valueAt * dRatio / 100;
                        }
                    }
                    itemAdd.PpValue = valPp;
                    itemAdd.Usage = structItem.Usage.Select(s => double.TryParse(s, out double result) ? result : 0.0).ToArray();
                    lock (lockObj)
                    {
                        lstResult.Add(itemAdd);
                    }
                }
            });
            if (product == "IJ")
            {
                _ctx.PcSimulationPartMasterChangeppIjs.AddRange(lstResult);
                await _ctx.SaveChangesAsync();
            }
            else if (product == "LBP")
            {
                var lstResult1 = lstResult.MapSimulationP();
                _ctx.PcSimulationPartMasterChangeppLbps.AddRange(lstResult1);
                await _ctx.SaveChangesAsync();
            }
            //email.Initial(lstMailReceive, " Completed Simulation part " + product + " output", @"<p style='font-weight: bold;'>Dear [Mrs/Mr]. All,</p>
            //    <p>You have received a notification about <span style='font-weight: bold; color: red; font-style: italic;'>Trial Run Simulation part - output Part master </span>from PSI System.</p>

            //    ");
        }
        public async Task PushLeadtime()
        {
            //var lstResult = new List<SimualationPartmasterByCellView>();
            var md1 = await _ctx.VSimulationLeadtimeLbpS1Findmers/*.Take(20)*/.ToListAsync();
            var lstDetail = await _ctx.VSimulationPartDemandByCellLbps.ToListAsync();
            var lstCell = await _ctx.VSimulationCountCellByShiftLbps.AsNoTracking().ToListAsync();
            var lstScanDate = new List<SimualationPartmasterTimeView>();
            foreach (var s in lstBlockTimeAll)
            {
                lstScanDate.Add(new SimualationPartmasterTimeView() { TimeF = s });
            }
            
            var lstLeadTime = await _ctx.VSimulationLeadtimeTableLbps.ToListAsync();
            ConcurrentDictionary<(string, string), (double,int)> dictLeadtime = new ConcurrentDictionary<(string, string), (double, int)>();
            foreach (var dtail in lstLeadTime)
            {
                dictLeadtime.AddOrUpdate((dtail.PartNo ?? "", dtail.Model ?? ""), (dtail.QtyKeepCell ?? 0, dtail.LeadtimeBlock.ObjToIntAble()), (key, oldValue) => (dtail.QtyKeepCell ?? 0, dtail.LeadtimeBlock.ObjToIntAble()));
            }
            ConcurrentDictionary<(string, string,string), int> dictCountCell = new ConcurrentDictionary<(string, string, string), int>();
            foreach (var c in lstCell)
            {
                dictCountCell.AddOrUpdate((c.ProductCode ?? "", c.PalletDate ?? "", c.Shift ?? ""), c.LineNo.ObjToIntAble(), (key, oldValue) => c.LineNo.ObjToIntAble());
            }
            ConcurrentDictionary<string, int> dicPositionMerchandise = new ConcurrentDictionary<string, int>();
            var header = await _ctx.TodStructureOutputHeaders.Where(x => x.Active == true && x.Product == "LBP")
                .Select(x => new { Model = x.Model, Merchandise = x.Merchandise, MerCode = x.MerCode })
                .AsNoTracking()
                .FirstAsync();
            foreach (var h in header.MerCode)
            {
                var arh = h.Split(':');
                dicPositionMerchandise.AddOrUpdate(arh[1], arh[0].ObjToIntAble(), (key, val) => arh[0].ObjToIntAble());
            }
            //Console.WriteLine(DateTime.Now.ToLongTimeString());
            var dtString = DateTime.Today.ToString("dd-MM-yyyy");
            var aiTime = await _ctx.VHksAiRestTimeLbps/*.Where(x => x.PalletDate == dtString)*/.AsNoTracking().ToListAsync();
            var lstDemandByCell = new List<SimualationPartmasterByCellView>();
            foreach (var p in md1)
            {
                var part = p.PartNo;
                var mer = p.ArrMerchandise;
                var lstDetail1 = lstDetail.Where(x => mer.Contains(x.Merchandise)).ToList();//*usage*ratio/100
                if(lstDetail1 == null || lstDetail1.Count < 1)
                {
                    continue;
                }
                ConcurrentDictionary<(string, string, string, string), double> dictQty = new ConcurrentDictionary<(string, string, string, string), double>();
                ConcurrentDictionary<string, double> dictUsage = new ConcurrentDictionary<string, double>();
                foreach (var m in p.ArrMerchandise.Distinct())
                {
                    if(dicPositionMerchandise.TryGetValue(m, out int usagePos))
                    {
                        double valUsage = p.Usage[usagePos].ObjToDoubleAble() * p.Ratio.ObjToDoubleAble() / 100;
                        dictUsage.TryAdd(m, valUsage);
                    }
                }
                
                foreach (var dtail in lstDetail1)
                {
                    dictQty.AddOrUpdate((dtail.Model ?? "", dtail.PalletDate ?? "", dtail.LineNo ?? "", dtail.BTime ?? ""), (dtail.Vqty ?? 0) * dictUsage[dtail.Merchandise], (key, oldValue) => oldValue + (dtail.Vqty ?? 0) * dictUsage[dtail.Merchandise]);
                }
                var lstLine = lstDetail1.Select(x => new { x.Model, x.LineNo }).Distinct().ToList();
                var lstLineShift = lstDetail1.Select(x => new { x.Model, x.LineNo, x.Shift }).Distinct().ToList();
                foreach (var l in lstLine)
                {
                    var aiItem = aiTime.Where(a => a.LineNo.StartsWith(l.LineNo)).ToList();
                    if(aiItem == null || aiTime.Count < 1)
                    {
                        continue;
                    }
                    var lstBlockRangeRestTime = aiItem.Where(a => a.RestTime != null && a.RestTime.Length > 0).SelectMany(a => a.RestTime).ToArray();
                    if(lstBlockRangeRestTime == null || lstBlockRangeRestTime.Length < 1)
                    {
                        continue;
                    }
                    var lstBreakEachBlockTime = lstBlockRangeRestTime.ReturnRestBlockTime();
                    double[] demand = new double[lstScanDate.Count];
                    int[] cellQty = new int[lstScanDate.Count];
                    bool[] hasPlan = new bool[lstScanDate.Count];
                    double[] leadTime = new double[lstScanDate.Count];
                    double[] qtyKeepCell = new double[lstScanDate.Count];
                    double[] totalLeadtime = new double[lstScanDate.Count];
                    var lstShift = lstLineShift.Where(b => b.Model == l.Model && b.LineNo == l.LineNo).Select(b => b.Shift).ToList();
                    dictLeadtime.TryGetValue((part, l.Model), out var b);
                    for (int i = 0; i < lstScanDate.Count; i++)
                    {
                        var d = lstScanDate[i];
                        var stBlock = d.BTime.Substring(11, 5);
                        if(lstBreakEachBlockTime.Contains(stBlock))
                        {
                            hasPlan[i] = false;
                            continue;
                        }
                        else
                        {
                            dictQty.TryGetValue((l.Model, d.PalletDate, l.LineNo, d.BTime), out demand[i]);
                            hasPlan[i] = lstShift.Contains(d.Shift);
                            dictCountCell.TryGetValue((l.Model, d.PalletDate, d.Shift), out cellQty[i]);
                        }
                    }
                    ReturnTotalLeadTimeBlock(demand, hasPlan, b.Item1, b.Item2, cellQty, ref leadTime, ref qtyKeepCell, ref totalLeadtime);
                    lstDemandByCell.Add(new SimualationPartmasterByCellView()
                    {
                        Cell = l.LineNo,
                        PartNo = part,
                        DemandArr = demand,
                        Model = l.Model,
                        HasPlanArr = hasPlan,
                        QtyKeepCell = b.Item1,
                        LeadtimeBlock = b.Item2,
                        LeadtimeArr = leadTime,
                        QtyKeepCellArr = qtyKeepCell,
                        TotalLeadTimeArr = totalLeadtime,
                        Vendor = p.Vendor,
                        RatioChangeRatio = p.RatioChange,
                        RatioChangeEffectiveDate = p.EffectivedateChange,
                        Ratio = p.Ratio,
                        PicPo = p.PoPic,
                        PicDo = p.DoPic,
                        Bc = p.Bc,
                        Dim = p.Dim,
                        PartName = p.PartName
                    });
                    Console.WriteLine($"Da tinh xong line {l.LineNo}: {DateTime.Now.ToString("HH:mm:ss.fff")}");
                }
                //Console.WriteLine($"Da tinh xong part {part}: {DateTime.Now.ToString("HH:mm:ss.fff")}");
            }
            Console.WriteLine($"Da tinh xong all part: {DateTime.Now.ToString("HH:mm:ss.fff")}");
            if(lstDemandByCell != null && lstDemandByCell.Count > 0)
            {
                var lstResult = new List<PcSimulationLeadtimeLbp>();
                foreach(var itcd in lstDemandByCell)
                {
                    lstResult.Add(new PcSimulationLeadtimeLbp()
                    {
                        PartNo = itcd.PartNo,
                        Model = itcd.Model,
                        Leadtime = itcd.LeadtimeBlock,
                        TotalLt = itcd.TotalLeadTimeArr,
                        QtyKeepCell = itcd.QtyKeepCell,
                        QtyKeepArr = itcd.QtyKeepCellArr,
                        LtValue = itcd.LeadtimeArr,
                        DateVal = lstBlockTimeAll,
                        DayQty = 12,
                        HasPlan = itcd.HasPlanArr,
                        Bc = itcd.Bc,
                        Destination = "",
                        Cell = itcd.Cell,
                        Dim = itcd.Dim,
                        PartName = itcd.PartName,
                        PicDo = itcd.PicDo,
                        PicPo = itcd.PicPo,
                        Ratio = itcd.Ratio,
                        RatioChangeEffectiveDate = itcd.RatioChangeEffectiveDate,
                        RatioChangeRatio = itcd.RatioChangeRatio,
                        Vendor = itcd.Vendor,
                        DemandArr = itcd.DemandArr
                    });
                }
                await _ctx.PcSimulationLeadtimeLbps.AddRangeAsync(lstResult);
                await _ctx.SaveChangesAsync();
            }
            
        }
        public async Task PushOp5NpisDoPo()
        {
            try
            {
                ADO.doChange("delete FROM public.pc_simulation_npis_pdo_lbp;delete FROM public.pc_simulation_npis_pdo_ij;");
                var lstReceiving = await _ctx.VReceivingSimulationOp5s.Select(x => new PcSimulationNpisPdoLbp()
                {
                    DateDelivery = x.DateDelivery,
                    DeliveryKey = x.DeliveryKey,
                    Location = x.Location,
                    PartNo = x.PartNo ?? "",
                    Vendor = x.Vendor ?? "",
                    PlanQty = x.QtyPlan.ObjToDoubleAble(),
                    TimeDelivery = x.TimeDelivery,
                    PoStatus = x.PoStatus
                }).ToListAsync();
                _ctx.PcSimulationNpisPdoLbps.AddRange(lstReceiving);
                await _ctx.SaveChangesAsync();
            }
            catch (Exception)
            {

            }
        }
        public async Task PushOp6SimulationTime(string product)
        {
            try
            {
                await ADO.doChangeAsync($"delete FROM public.pc_simulation_sm_time_op6_{product.ToLower()}");
                var vStructureModel = await _ctx.VSimulationTimeOp6StructureLbps.AsNoTracking().ToListAsync();
                var vDoModel = await _ctx.VSimulationTimeOp6DoLbps.AsNoTracking().ToListAsync();
                var vPartMaster = await _ctx.PcSimulationPartMasterLbps.Where(x => x.Active == true).AsNoTracking().ToListAsync();
                var vPartMasterC = await _ctx.PcSimulationPartMasterChangeppLbps.Where(x => x.Active == true).AsNoTracking().ToListAsync();
                var pStructureModel = Partitioner.Create(vStructureModel);
                var lstResult = new List<PcSimulationSmTimeOp6Lbp>();
                pStructureModel.AsParallel().ForAll(Item =>
                {
                    var pmi = vPartMaster.Where(x => x.PartNo == Item.PartNo && x.Vendor == Item.Vendor).FirstOrDefault();
                    var pmic = vPartMasterC.Where(x => x.PartNo == Item.PartNo && x.Vendor == Item.Vendor).FirstOrDefault();
                    var doi = vDoModel.Where(x => x.PartNo == Item.PartNo && x.Vendor == Item.Vendor).FirstOrDefault();
                    if (pmi != null)
                    {
                        var itemAdd = new PcSimulationSmTimeOp6Lbp()
                        {
                            Bc = Item.Bc ?? "",
                            PartNo = Item.PartNo ?? "",
                            Dim = Item.Dim,
                            PartName = Item.PartName ?? "",
                            Vendor = Item.Vendor ?? "",
                            Model = Item.Model ?? "",
                            Destination = Item.Destination ?? "",
                            Factory = Item.Factory ?? "",
                            Ratio = Item.Ratio ?? "",
                            RatioChange = Item.RatioChange ?? "",
                            EffectivedateChange = Item.EffectiveDateChange ?? "",
                            DoPic = Item.DoPic ?? "",
                            PoPic = Item.PoPic ?? "",
                            PartMovingRoute = Item.PartMovingRoute ?? "",
                            MoqOrder = Item.MoqOrder.ObjToStringAble(),
                            MainAlt = Item.MainAlt ?? "",
                            Inventory = 0,
                            BlockTime = lstBlockTimeAll
                        };
                        itemAdd.Demand = pmi.PpValue;
                        if(pmic != null)
                        {
                            itemAdd.DemandCp = pmic.PpValue;
                        }
                        //if(doi != null)
                        //{
                        //    itemAdd.DoVal = doi.
                        //}
                    }
                });
            }
            catch (Exception)
            {

            }
        }
        public async Task ExportOp5NpisDoPo()
        {
            var lstModel = await _ctx.PcSimulationNpisPdoLbps.ToListAsync();
            if(lstModel.Count < 1)
            {
                return;
            }
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (ExcelPackage excelPackage = new ExcelPackage())
            {
                ExcelWorksheet excelWorksheet;
                excelWorksheet = excelPackage.Workbook.Worksheets.Add("NPIS DO PO");
                var numberRowTotal = 0;
                int currentRow = 2;
                for (int i = 0; i < lstModel.Count; i++)
                {
                    var item = lstModel[i];
                    excelWorksheet.Cells["A" + currentRow].Value = (i + 1);
                    excelWorksheet.Cells["B" + currentRow].Value = item.PartNo;
                    excelWorksheet.Cells["C" + currentRow].Value = item.Vendor;
                    excelWorksheet.Cells["D" + currentRow].Value = item.DeliveryKey;
                    excelWorksheet.Cells["E" + currentRow].Value = item.Location;
                    excelWorksheet.Cells["F" + currentRow].Value = item.DateDelivery;
                    excelWorksheet.Cells["G" + currentRow].Value = item.TimeDelivery;
                    excelWorksheet.Cells["H" + currentRow].Value = item.PlanQty;
                    excelWorksheet.Cells["I" + currentRow].Value = item.PoStatus;
                    currentRow++;
                }
                numberRowTotal += lstModel.Count;
                excelWorksheet.Cells[1, 1, numberRowTotal + 1, 9].Style.Font.Size = 12;
                excelWorksheet.Cells[1, 1, numberRowTotal + 1, 9].Style.Font.Name = "Calibri";
                excelWorksheet.Cells[1, 1, numberRowTotal + 1, 9].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[1, 1, numberRowTotal + 1, 9].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[1, 1, numberRowTotal + 1, 9].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                excelWorksheet.Cells[1, 1, numberRowTotal + 1, 9].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                {
                    excelWorksheet.Cells["A1"].Value = "No";
                    excelWorksheet.Cells["B1"].Value = "Part No";
                    excelWorksheet.Cells["C1"].Value = "Vendor";
                    excelWorksheet.Cells["D1"].Value = "Delivery Key";
                    excelWorksheet.Cells["E1"].Value = "Location";
                    excelWorksheet.Cells["F1"].Value = "Date Delivery";
                    excelWorksheet.Cells["G1"].Value = "Time Delivery";
                    excelWorksheet.Cells["H1"].Value = "Qty Plan";
                    excelWorksheet.Cells["I1"].Value = "Po Status";
                }

                string outputPath = this.outputPathOri + @"\7. Digital Simulation\Output\OP5.Simulation " + DateTime.Now.ToString("yyyy-MM-dd HHmm") + " NPIS PO DO.xlsx";
                FileInfo excelFile = new FileInfo(outputPath);
                excelPackage.SaveAs(excelFile);
                excelPackage.Dispose();

                //email.Initial(lstMailReceive, " Completed Simulation part " + product + " output", @"<p style='font-weight: bold;'>Dear [Mrs/Mr]. All,</p>
                //<p>You have received a notification about <span style='font-weight: bold; color: red; font-style: italic;'>Trial EXPORT Simulation part - output Live PP block 10 minutes </span>from PSI System. Please check at <a href='file:" + this.outputPathOri + @"\7. Digital Simulation\Output'>Export Folder</a> .</p>

                //");
            }
        }
        
        public async Task PushBlockTimeByDate(string product)
        {

            var lstDate12Workingdaty = new List<DateOnly?>();
            if (product == "IJ")
            {
                lstDate12Workingdaty = await _ctx.VWorkingDateIj12s.AsNoTracking().Select(x => x.DateOfDate).ToListAsync();
            }
            else if (product == "LBP")
            {
                lstDate12Workingdaty = await _ctx.VWorkingDateLbp12s.AsNoTracking().Select(x => x.DateOfDate).ToListAsync();
            }
            var lstTime = new List<DateTime>();
            var lstBlockTimeSimple2 = lstBlockTimeSimple.Select(x => x.StartTime).ToList();
            for (int i = 0; i < lstDate12Workingdaty.Count; i++)
            {
                var itoday = lstDate12Workingdaty[i].Value;
                var itomorrow = lstDate12Workingdaty[i].Value.AddDays(1);
                foreach (var itemBlock in lstBlockTimeSimple2)
                {
                    if (itemBlock.Hour > 7)
                    {
                        lstTime.Add(new DateTime(itoday.Year, itoday.Month, itoday.Day, itemBlock.Hour, itemBlock.Minute, itemBlock.Second));
                    }
                    else
                    {
                        lstTime.Add(new DateTime(itomorrow.Year, itomorrow.Month, itomorrow.Day, itemBlock.Hour, itemBlock.Minute, itemBlock.Second));
                    }
                }
            }
            var model = new PcSimulationDetailBlocktimeBydate()
            {
                NumberDay = lstDate12Workingdaty.Count,
                BlockTime = lstTime.ToArray(),
                Product = product
            };
            _ctx.PcSimulationDetailBlocktimeBydates.Add(model);
            await _ctx.SaveChangesAsync();
        }
        private void ReturnTotalLeadTimeBlock(double[] PpValue, bool[] HasPlan, double QtyKeepCell, int LeadtimeBlock, int[] cellQty, ref double[] leadTime, ref double[] qtyKeepCell, ref double[] totalLeadtime)
        {
            var arrActive = HasPlan.Select((value, index) => new { value, index })
                      .Where(item => item.value == true)
                      .Select(item => item.index)
                      .ToArray();
            for (int i = 0; i < arrActive.Length; i++)
            {
                qtyKeepCell[arrActive[i]] = QtyKeepCell * cellQty[i];
                double sum = 0;
                for (int j = i; j < i + LeadtimeBlock; j++)
                {
                    try
                    {
                        sum += PpValue[arrActive[j]];
                    }
                    catch { };
                }
                leadTime[arrActive[i]] = sum;
                totalLeadtime[arrActive[i]] += sum + QtyKeepCell * cellQty[i];
            }
        }
        
    }
}
