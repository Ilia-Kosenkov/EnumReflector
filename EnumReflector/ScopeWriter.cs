using System;
using System.Text;

namespace EnumReflector
{
    internal sealed class ScopeWriter : IDisposable
    {
        private readonly StringBuilder _builder;
        public int Depth { get; }
        public bool PutSemicolon { get; }


        public ScopeWriter(StringBuilder builder, int depth = 0, bool putSemicolon = false)
        {
            Depth = depth;
            _builder = builder;
            PutSemicolon = putSemicolon;
            _builder.AppendLine(new string('\t', Depth) + '{');
        }
        public void Dispose()
        {
            _builder.AppendLine($"{new string('\t', Depth)}}}{(PutSemicolon ? ";" : "")}");
        }
    }
}
