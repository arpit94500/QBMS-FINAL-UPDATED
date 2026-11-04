using System;
using System.Collections.Generic;

namespace QBMS.Application.Models
{
    public class ActivityLogEntry
    {
        public long LogId { get; set; }
        public DateTime LoggedAt { get; set; }
        public long? UserId { get; set; }
        public string PersonnelNo { get; set; }
        public string UserName { get; set; }
        public string Role { get; set; }
        public string Activity { get; set; }       // Controller/Action, LOGIN_SUCCESS, LOGIN_FAILED
        public string HttpMethod { get; set; }
        public string Path { get; set; }
        public string QueryString { get; set; }
        public string IpAddress { get; set; }
        public bool IsSuccess { get; set; } = true;
        public string Details { get; set; }
    }

    public class ActivityLogQuery
    {
        public string PersonnelNo { get; set; }
        public string Role { get; set; }
        public string Search { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class ActivityLogPage
    {
        public ActivityLogQuery Query { get; set; } = new ActivityLogQuery();
        public List<ActivityLogEntry> Items { get; set; } = new List<ActivityLogEntry>();
        public int Total { get; set; }
        public int TotalPages { get { return Query.PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)Query.PageSize)); } }
    }
}
