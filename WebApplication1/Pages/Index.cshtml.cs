using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OfficeOpenXml;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;

public class IndexModel : PageModel
{
    public class Message
    {
        public string From { get; set; }
        public string Text { get; set; }
    }

    public List<Message> ChatMessages { get; set; } = new();
    public List<string> Options { get; set; } = new();

    private static DataTable _issueTable;

    static IndexModel()
    {
        try
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "issues.xlsx");
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[0];
            var dt = new DataTable();

            foreach (var headerCell in worksheet.Cells[1, 1, 1, worksheet.Dimension.End.Column])
                dt.Columns.Add(headerCell.Text);

            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                var dataRow = dt.NewRow();
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                    dataRow[col - 1] = worksheet.Cells[row, col].Text;
                dt.Rows.Add(dataRow);
            }

            _issueTable = dt;
        }
        catch (Exception ex)
        {
           // File.WriteAllText("error_log.txt", ex.ToString());
        }
    }

    public void OnGet()
    {
        ChatMessages = new List<Message>
        {
            new() { From = "bot", Text = "Hi! How can I help you today?" }
        };

        Options = _issueTable.AsEnumerable()
            .Where(r => string.IsNullOrWhiteSpace(r["ParentID"].ToString()))
            .Select(r => r["Label"].ToString())
            .ToList();

        TempData["ChatHistory"] = JsonSerializer.Serialize(ChatMessages);
    }

    public void OnPost(string selectedOption)
    {
        // Restore chat
        var rawChat = TempData["ChatHistory"] as string;
        ChatMessages = string.IsNullOrEmpty(rawChat)
            ? new List<Message>()
            : JsonSerializer.Deserialize<List<Message>>(rawChat);

        ChatMessages.Add(new Message { From = "user", Text = selectedOption });

        var selectedRow = _issueTable.AsEnumerable()
            .FirstOrDefault(r => r["Label"].ToString() == selectedOption);

        if (selectedRow != null)
        {
            var solution = selectedRow["Solution"].ToString();
            if (!string.IsNullOrWhiteSpace(solution))
            {
                ChatMessages.Add(new Message { From = "bot", Text = solution });
                Options = new();
            }
            else
            {
                var id = selectedRow["IssueID"].ToString();
                Options = _issueTable.AsEnumerable()
                    .Where(r => r["ParentID"].ToString() == id)
                    .Select(r => r["Label"].ToString())
                    .ToList();

                ChatMessages.Add(new Message
                {
                    From = "bot",
                    Text = "Please choose one of the following options:"
                });
            }
        }

        TempData["ChatHistory"] = JsonSerializer.Serialize(ChatMessages);
    }
}
