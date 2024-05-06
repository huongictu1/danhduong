
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PDCProjectApi.Common;
using PDCProjectApi.Data;
using PDCProjectApi.Model.View;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

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

    public class SimulationJob : ISimulation
    {
        private readonly PdcsystemContext _ctx;
        private readonly IEmailService _emailService;
        private readonly List<string> _lstMailReceive;
        private readonly string _outputPath;

        public SimulationJob(PdcsystemContext ctx, IEmailService emailService, IGlobalVariable globalVariable)
        {
            _ctx = ctx;
            _emailService = emailService;
            _lstMailReceive = globalVariable.ReturnPDCMail();
            _outputPath = globalVariable.ReturnPathOutput();
        }

        public async Task ExportPartMaster(string product, bool changepp)
        {
            var headers = await _ctx.TodStructureOutputHeaders
                                    .Where(x => x.Active && x.Product == product)
                                    .Select(x => new { x.Model, x.Merchandise, x.MerCode })
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync();

            if (headers == null) return;

            var partMasters = await _ctx.PcSimulationPartMasters
                                        .Where(x => x.Product == product && x.ChangePP == changepp)
                                        .ToListAsync();

            if (!partMasters.Any()) return;

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Part Master Export");

            // Add headers and style them
            AddHeadersAndStyle(worksheet, headers);

            int rowIndex = 2;
            foreach (var partMaster in partMasters)
            {
                worksheet.Cells[rowIndex, 1].Value = partMaster.PartNo;
                worksheet.Cells[rowIndex, 2].Value = partMaster.Merchandise;
                worksheet.Cells[rowIndex, 3].Value = partMaster.Quantity;
                rowIndex++;
            }

            worksheet.Cells.AutoFitColumns();
            var filePath = System.IO.Path.Combine(_outputPath, "PartMasterExport.xlsx");
            package.SaveAs(new FileInfo(filePath));

            _emailService.SendEmail(_lstMailReceive, "Part Master Export Completed", $"The part master export for {product} has been completed.");
        }

        private void AddHeadersAndStyle(ExcelWorksheet worksheet, dynamic headers)
        {
            worksheet.Cells[1, 1].Value = "Part No";
            worksheet.Cells[1, 2].Value = "Merchandise";
            worksheet.Cells[1, 3].Value = "Quantity";
            worksheet.Cells["A1:C1"].Style.Font.Bold = true;
            worksheet.Cells["A1:C1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells["A1:C1"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        }

        // Implement other methods with similar enhancements...

    }
}
