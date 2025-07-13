using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OfficeOpenXml;
using System.Data;
using System.Diagnostics;
using System.Text.Json;

namespace WebApplication1.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        private readonly ILogger<ErrorModel> _logger;

        public ErrorModel(ILogger<ErrorModel> logger)
        {
            _logger = logger;
        }


        public class Message
        {
            public string From { get; set; }
            public string Text { get; set; }
        }



        [BindProperty]
        public string UserQuestion { get; set; }

        public List<string> Options { get; set; } = new();
        public List<Message> ChatMessages { get; set; } = new();



        private static DataTable _issueTable;


        static ErrorModel()
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
            ChatMessages.Add(new Message { From = "bot", Text = "Hello! How can I help you today?" });




            Options = _issueTable.AsEnumerable()
                .Where(r => string.IsNullOrWhiteSpace(r["ParentID"].ToString()))
                .Select(r => r["Label"].ToString())
                .ToList();

            TempData["ChatHistory"] = JsonSerializer.Serialize(ChatMessages);


        }

        public IActionResult OnPostAsk()
        {
            ChatMessages.Clear();
            ChatMessages.Add(new Message { From = "bot", Text = "Hello! How can I help you today?" });

            ChatMessages.Add(new Message { From = "user", Text = UserQuestion });

            // Simulated search - replace with actual logic
            var matchedOptions = SearchIssues(UserQuestion);

            if (matchedOptions.Any())
            {
                Options = matchedOptions;
                ChatMessages.Add(new Message { From = "bot", Text = "Please select one of the options below:" });
            }
            else
            {
                ChatMessages.Add(new Message { From = "bot", Text = "Sorry, no matching issues found." });
            }
            TempData["ChatHistory"] = JsonSerializer.Serialize(ChatMessages);

            return Page();
        }


        public void OnPostOption(string selectedOption)
        {

            if (selectedOption.Equals("An unknown charge"))
            {
                ChatMessages.Clear();
                ChatMessages.Add(new Message { From = "bot", Text = "Hello! How can I help you today?" });



                // Simulated search - replace with actual logic

                Options = new List<string>();
            }
            else
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
            }




            TempData["ChatHistory"] = JsonSerializer.Serialize(ChatMessages);
        }


        public IActionResult OnPost()
        {
            var selected = Request.Form["selectedOption"];
            ChatMessages.Add(new Message { From = "user", Text = selected });
            ChatMessages.Add(new Message { From = "bot", Text = $"You selected: {selected}" });

            // Clear options after selection
            Options.Clear();

            return Page();
        }

        private List<string> SearchIssues(string question)
        {
            var results = new List<string>();

            var words = question
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(w => w.ToLowerInvariant()).Where(x => x.Length > 2)
                .ToList();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "issues.xlsx");

            if (!System.IO.File.Exists(filePath))
                return results;

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                if (worksheet == null) return results;

                // Find the "Label" column index (header assumed in row 1)
                int labelColumnIndex = -1;
                int colCount = worksheet.Dimension.Columns;

                for (int col = 1; col <= colCount; col++)
                {
                    var header = worksheet.Cells[1, col].Text.Trim();
                    if (string.Equals(header, "Label", StringComparison.OrdinalIgnoreCase))
                    {
                        labelColumnIndex = col;
                        break;
                    }
                }

                if (labelColumnIndex == -1)
                    return results; // No "Label" column found

                int rowCount = worksheet.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++) // Skip header
                {
                    string label = worksheet.Cells[row, labelColumnIndex].Text.Trim();
                    if (words.Any(word => label.ToLower().Contains(word)))
                    {
                        results.Add(label);
                    }
                }
            }

            return results.Distinct().ToList();
        }

    }

}
