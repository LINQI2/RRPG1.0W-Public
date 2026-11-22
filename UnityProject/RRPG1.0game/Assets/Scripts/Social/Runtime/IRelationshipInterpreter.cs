using System.Collections.Generic;
namespace RRPG.Social
{
    /// <summary>Pure planning contract. WP-AL-1 supplies no production interpretation rule.</summary>
    public interface IRelationshipInterpreter
    {
        IEnumerable<RelationshipMutation> Interpret(RelationalFactRecord fact, IRelationshipQuery relationships);
    }
}
