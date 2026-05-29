namespace SlotStrings
{
    /// <summary>A renderable string bound to an <see cref="ISlotStringHost"/>; output is cached until the host's state token changes.</summary>
    public class SlotString
    {
        private readonly ISlotStringHost _host;
        private readonly SlotStringTemplate _template;
        private int _stateToken;
        private string _output;

        /// <summary>Creates a <see cref="SlotString"/> from a raw template and host.</summary>
        public SlotString(string raw, ISlotStringHost host)
            : this(new SlotStringTemplate(raw), host)
        {
        }

        /// <summary>Creates a <see cref="SlotString"/> from a prebuilt template and host. Multiple instances may share one template.</summary>
        public SlotString(SlotStringTemplate template, ISlotStringHost host)
        {
            _host = host ?? throw new System.ArgumentNullException(nameof(host));
            _template = template ?? throw new System.ArgumentNullException(nameof(template));
            _stateToken = host.GetStateToken();
            _output = null;
        }

        /// <summary>Renders the template; returns cached output if the host's state token is unchanged, otherwise recomputes and refreshes the cache.</summary>
        public override string ToString()
        {
            if (_stateToken == _host.GetStateToken() && _output != null)
            {
                return _output;
            }

            return ToStringImpl();
        }

        /// <summary>Renders unconditionally, bypassing the cache-hit check; the fresh value is also written back to the cache.</summary>
        public string ToStringForce()
        {
            return ToStringImpl();
        }

        private string ToStringImpl()
        {
            _stateToken = _host.GetStateToken();
            _output = _template.Format(_host);
            return _output;
        }
    }
}
