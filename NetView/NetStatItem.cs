using System;


namespace NetViewer
{
    public class NetStatItem
    {
        public string Program { get; set; }
        public string SourceIp { get; set; }
        public string SourcePort { get; set; }
        public string TargetIp { get; set; }
        public string TargetPort { get; set; }
        public string Protocol { get; set; }
        public string ProgramPath { get; set; }
        public string Status { get; set; }
        public int Pid { get; set; }


        public string Suorce { get => $"{this.SourceIp}:{this.SourcePort}"; }
        public string Target { get => $"{this.TargetIp}:{this.TargetPort}"; }

    }
}
