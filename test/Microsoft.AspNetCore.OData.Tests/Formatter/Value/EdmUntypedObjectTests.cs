//-----------------------------------------------------------------------------
// <copyright file="EdmUntypedObjectTests.cs" company=".NET Foundation">
//      Copyright (c) .NET Foundation and Contributors. All rights reserved.
//      See License.txt in the project root for license information.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.AspNetCore.OData.Formatter.Value;
using Microsoft.OData.Edm;
using Xunit;

namespace Microsoft.AspNetCore.OData.Tests.Formatter.Value
{
    public class EdmUntypedObjectTests
    {
        [Fact]
        public void GetEdmType_OnEdmUntypedObject_Returns_EdmType()
        {
            // Arrange
            EdmUntypedObject untypedObj = new EdmUntypedObject();

            // Act
            IEdmTypeReference edmType = untypedObj.GetEdmType();

            // Assert
            Assert.Equal("Edm.Untyped", edmType.FullName());
        }
    }
}
