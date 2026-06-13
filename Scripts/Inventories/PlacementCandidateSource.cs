using System;
using System.Collections;
using System.Collections.Generic;

namespace UDND.Inventories
{
    public sealed class PlacementCandidateSource : IEnumerable<PlacementCandidate>
    {
        private readonly Func<IEnumerable<PlacementCandidate>> _enumerate;

        public PlacementCandidateSource(Func<IEnumerable<PlacementCandidate>> enumerate)
        {
            _enumerate = enumerate ?? throw new ArgumentNullException(nameof(enumerate));
        }

        public IEnumerator<PlacementCandidate> GetEnumerator()
            => (_enumerate() ?? Array.Empty<PlacementCandidate>()).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
