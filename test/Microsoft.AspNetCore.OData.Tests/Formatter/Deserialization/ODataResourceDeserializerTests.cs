//-----------------------------------------------------------------------------
// <copyright file="ODataResourceDeserializerTests.cs" company=".NET Foundation">
//      Copyright (c) .NET Foundation and Contributors. All rights reserved.
//      See License.txt in the project root for license information.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Edm;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Formatter.Deserialization;
using Microsoft.AspNetCore.OData.Formatter.Value;
using Microsoft.AspNetCore.OData.Formatter.Wrapper;
using Microsoft.AspNetCore.OData.Tests.Commons;
using Microsoft.AspNetCore.OData.Tests.Edm;
using Microsoft.AspNetCore.OData.Tests.Extensions;
using Microsoft.AspNetCore.OData.Tests.Models;
using Microsoft.Extensions.Logging;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using Microsoft.OData.ModelBuilder;
using Microsoft.OData.UriParser;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.OData.Tests.Formatter.Deserialization
{
    public class ODataResourceDeserializerTests
    {
        private static IEdmModel _edmModel = GetEdmModel();

        private readonly ODataDeserializerContext _readContext;
        private readonly IEdmEntityTypeReference _productEdmType;
        private readonly IEdmEntityTypeReference _supplierEdmType;
        private readonly IEdmComplexTypeReference _addressEdmType;
        private readonly IODataDeserializerProvider _deserializerProvider;
        private readonly string _supplyRequestResource;

        public ODataResourceDeserializerTests()
        {
            IEdmEntitySet entitySet = _edmModel.EntityContainer.FindEntitySet("Products");
            _readContext = new ODataDeserializerContext
            {
                Path = new ODataPath(new EntitySetSegment(entitySet)),
                Model = _edmModel,
                ResourceType = typeof(Product)
            };
            _productEdmType = _edmModel.GetEdmTypeReference(typeof(Product)).AsEntity();
            _supplierEdmType = _edmModel.GetEdmTypeReference(typeof(Supplier)).AsEntity();
            _addressEdmType = _edmModel.GetEdmTypeReference(typeof(Address)).AsComplex();
            _deserializerProvider = ODataFormatterHelpers.GetDeserializerProvider();

            _supplyRequestResource = @"{
                ""ID"":0,
                ""Name"":""Supplier Name"",
                ""Concurrency"":0,
                ""Address"":
                {
                    ""Street"":""Supplier Street"",
                    ""City"":""Supplier City"",
                    ""State"":""WA"",
                    ""ZipCode"":""123456"",
                    ""CountryOrRegion"":""USA""
                },
                Products:
                [
                    {
                        ""ID"":1,
                        ""Name"":""Milk"",
                        ""Description"":""Low fat milk"",
                        ""ReleaseDate"":""1995-10-01T00:00:00z"",
                        ""DiscontinuedDate"":null,
                        ""Rating"":3,
                        ""Price"":3.5
                    },
                    {
                        ""ID"":2,
                        ""Name"":""soda"",
                        ""Description"":""sample summary"",
                        ""ReleaseDate"":""1995-10-01T00:00:00z"",
                        ""DiscontinuedDate"":null,
                        ""Rating"":3,
                        ""Price"":20.9
                    },
                    {
                        ""ID"":3,
                        ""Name"":""Product3"",
                        ""Description"":""Product3 Summary"",
                        ""ReleaseDate"":""1995-10-01T00:00:00z"",
                        ""DiscontinuedDate"":null,
                        ""Rating"":3,
                        ""Price"":19.9
                    },
                    {
                        ""ID"":4,
                        ""Name"":""Product4"",
                        ""Description"":""Product4 Summary"",
                        ""ReleaseDate"":""1995-10-01T00:00:00z"",
                        ""DiscontinuedDate"":null,
                        ""Rating"":3,
                        ""Price"":22.9
                    },
                    {
                        ""ID"":5,
                        ""Name"":""Product5"",
                        ""Description"":""Product5 Summary"",
                        ""ReleaseDate"":""1995-10-01T00:00:00z"",
                        ""DiscontinuedDate"":null,
                        ""Rating"":3,
                        ""Price"":22.8
                    },
                    {
                        ""ID"":6,
                        ""Name"":""Product6"",
                        ""Description"":""Product6 Summary"",
                        ""ReleaseDate"":""1995-10-01T00:00:00z"",
                        ""DiscontinuedDate"":null,
                        ""Rating"":3,
                        ""Price"":18.8
                    }
                ]
            }";
        }

        [Fact]
        public void Ctor_ThrowsArgumentNull_DeserializerProvider()
        {
            ExceptionAssert.ThrowsArgumentNull(() => new ODataResourceDeserializer(deserializerProvider: null), "deserializerProvider");
        }

        [Fact]
        public async Task ReadAsync_ThrowsArgumentNull_MessageReader()
        {
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            await ExceptionAssert.ThrowsArgumentNullAsync(
                () => deserializer.ReadAsync(messageReader: null, type: typeof(Product), readContext: _readContext),
                "messageReader");
        }

        [Fact]
        public async Task ReadAsync_ThrowsArgumentNull_ReadContext()
        {
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            await ExceptionAssert.ThrowsArgumentNullAsync(
                () => deserializer.ReadAsync(messageReader: ODataFormatterHelpers.GetMockODataMessageReader(), type: typeof(Product), readContext: null),
                "readContext");
        }

        [Fact]
        public void ReadAsync_ThrowsArgument_ODataPathMissing_ForEntity()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataDeserializerContext readContext = new ODataDeserializerContext
            {
                Model = _edmModel,
                ResourceType = typeof(Product)
            };

            // Act & Assert
            ExceptionAssert.ThrowsArgument(
                () => deserializer.ReadAsync(ODataFormatterHelpers.GetMockODataMessageReader(), typeof(Product), readContext).Wait(),
                "readContext",
                "The operation cannot be completed because no ODataPath is available for the request.");
        }

        [Fact]
        public async Task ReadAsync_ThrowsArgument_EntitysetMissing()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataDeserializerContext readContext = new ODataDeserializerContext
            {
                Path = new ODataPath(),
                Model = _edmModel,
                ResourceType = typeof(Product)
            };

            // Act & Assert
            await ExceptionAssert.ThrowsAsync<SerializationException>(
                () => deserializer.ReadAsync(ODataFormatterHelpers.GetMockODataMessageReader(), typeof(Product), readContext),
                "The related entity set or singleton cannot be found from the OData path. The related entity set or singleton is required to deserialize the payload.");
        }

        [Fact]
        public void ReadInline_ThrowsArgumentNull_Item()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNull(
                () => deserializer.ReadInline(item: null, edmType: _productEdmType, readContext: new ODataDeserializerContext()),
                "item");
        }

        [Fact]
        public void ReadInline_ThrowsArgumentNull_EdmType()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNull(
                () => deserializer.ReadInline(item: new object(), edmType: null, readContext: new ODataDeserializerContext()),
                "edmType");
        }

        [Fact]
        public void ReadInline_Throws_ArgumentMustBeOfType()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act & Assert
            ExceptionAssert.ThrowsArgument(
                () => deserializer.ReadInline(item: 42, edmType: _productEdmType, readContext: new ODataDeserializerContext()),
                "item",
                "The argument must be of type 'ODataResource'");
        }

        [Fact]
        public void ReadInline_Calls_ReadResource()
        {
            // Arrange
            var deserializer = new Mock<ODataResourceDeserializer>(_deserializerProvider);
            ODataResourceWrapper entry = new ODataResourceWrapper(new ODataResource());
            ODataDeserializerContext readContext = new ODataDeserializerContext();

            deserializer.CallBase = true;
            deserializer.Setup(d => d.ReadResource(entry, _productEdmType, readContext)).Returns(42).Verifiable();

            // Act
            var result = deserializer.Object.ReadInline(entry, _productEdmType, readContext);

            // Assert
            deserializer.Verify();
            Assert.Equal(42, result);
        }

        [Fact]
        public void ReadResource_ThrowsArgumentNull_ResourceWrapper()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNull(
                () => deserializer.ReadResource(resourceWrapper: null, structuredType: _productEdmType, readContext: _readContext),
                "resourceWrapper");
        }

        [Fact]
        public void ReadResource_ThrowsArgumentNull_ReadContext()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(new ODataResource());

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNull(
                () => deserializer.ReadResource(resourceWrapper, structuredType: _productEdmType, readContext: null),
                "readContext");
        }

        [Fact]
        public void ReadResource_ThrowsArgument_ModelMissingFromReadContext()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(new ODataResource { TypeName = _supplierEdmType.FullName() });

            // Act & Assert
            ExceptionAssert.ThrowsArgument(
                () => deserializer.ReadResource(resourceWrapper, _productEdmType, new ODataDeserializerContext()),
                "readContext",
                "The EDM model is missing on the read context. The model is required on the read context to deserialize the payload.");
        }

        [Fact]
        public void ReadResource_ThrowsODataException_EntityTypeNotInModel()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataResourceWrapper entry = new ODataResourceWrapper(new ODataResource { TypeName = "MissingType" });

            // Act & Assert
            ExceptionAssert.Throws<ODataException>(
                () => deserializer.ReadResource(entry, _productEdmType, _readContext),
                "Cannot find the resource type 'MissingType' in the model.");
        }

        [Fact]
        public void ReadResource_ThrowsODataException_CannotInstantiateAbstractResourceType()
        {
            // Arrange
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
            builder.EntityType<BaseType>().Abstract();
            IEdmModel model = builder.GetEdmModel();
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataResourceWrapper resourceWrapper =
                new ODataResourceWrapper(new ODataResource
                {
                    TypeName = "Microsoft.AspNetCore.OData.Tests.Formatter.Deserialization.BaseType"
                });

            // Act & Assert
            ExceptionAssert.Throws<ODataException>(
                () => deserializer.ReadResource(resourceWrapper, _productEdmType, new ODataDeserializerContext { Model = model }),
                "An instance of the abstract resource type 'Microsoft.AspNetCore.OData.Tests.Formatter.Deserialization.BaseType' was found. " +
                "Abstract resource types cannot be instantiated.");
        }

        [Fact]
        public void ReadResource_ThrowsSerializationException_TypeCannotBeDeserialized()
        {
            // Arrange
            Mock<IODataDeserializerProvider> deserializerProvider = new Mock<IODataDeserializerProvider>();
            deserializerProvider.Setup(d => d.GetEdmTypeDeserializer(It.IsAny<IEdmTypeReference>(), false)).Returns<ODataEdmTypeDeserializer>(null);
            var deserializer = new ODataResourceDeserializer(deserializerProvider.Object);
            ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(new ODataResource { TypeName = _supplierEdmType.FullName() });

            // Act & Assert
            ExceptionAssert.Throws<SerializationException>(
                () => deserializer.ReadResource(resourceWrapper, _productEdmType, _readContext),
                "'ODataDemo.Supplier' cannot be deserialized using the OData input formatter.");
        }

        [Fact]
        public void ReadResource_DispatchesToRightDeserializer_IfEntityTypeNameIsDifferent()
        {
            // Arrange
            Mock<ODataEdmTypeDeserializer> supplierDeserializer = new Mock<ODataEdmTypeDeserializer>(ODataPayloadKind.Resource);
            Mock<IODataDeserializerProvider> deserializerProvider = new Mock<IODataDeserializerProvider>();
            var deserializer = new ODataResourceDeserializer(deserializerProvider.Object);
            ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(new ODataResource { TypeName = _supplierEdmType.FullName() });

            deserializerProvider.Setup(d => d.GetEdmTypeDeserializer(It.IsAny<IEdmTypeReference>(), false)).Returns(supplierDeserializer.Object);
            supplierDeserializer
                .Setup(d => d.ReadInline(resourceWrapper, It.Is<IEdmTypeReference>(e => _supplierEdmType.Definition == e.Definition), _readContext))
                .Returns(42).Verifiable();

            // Act
            object result = deserializer.ReadResource(resourceWrapper, _productEdmType, _readContext);

            // Assert
            supplierDeserializer.Verify();
            Assert.Equal(42, result);
        }

        /*
        [Fact]
        public void ReadResource_SetsExpectedAndActualEdmType_OnCreatedEdmObject_TypelessMode()
        {
            // Arrange
            CustomersModelWithInheritance model = new CustomersModelWithInheritance();
            IEdmEntityTypeReference customerType = EdmLibHelpers.ToEdmTypeReference(model.Customer, isNullable: false).AsEntity();
            ODataDeserializerContext readContext = new ODataDeserializerContext { Model = model.Model, ResourceType = typeof(IEdmObject) };
            ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(new ODataResource
            {
                TypeName = model.SpecialCustomer.FullName(),
                Properties = new ODataProperty[0]
            });

            ODataResourceDeserializer deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act
            var result = deserializer.ReadResource(resourceWrapper, customerType, readContext);

            // Assert
            EdmEntityObject resource = Assert.IsType<EdmEntityObject>(result);
            Assert.Equal(model.SpecialCustomer, resource.ActualEdmType);
            Assert.Equal(model.Customer, resource.ExpectedEdmType);
        }*/

        [Fact]
        public void ReadResource_Calls_CreateResourceInstance()
        {
            // Arrange
            Mock<ODataResourceDeserializer> deserializer = new Mock<ODataResourceDeserializer>(_deserializerProvider);
            ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(new ODataResource { Properties = Enumerable.Empty<ODataProperty>() });
            deserializer.CallBase = true;
            deserializer.Setup(d => d.CreateResourceInstance(_productEdmType, _readContext)).Returns(42).Verifiable();

            // Act
            var result = deserializer.Object.ReadResource(resourceWrapper, _productEdmType, _readContext);

            // Assert
            Assert.Equal(42, result);
            deserializer.Verify();
        }

        [Fact]
        public void ReadResource_Calls_ApplyStructuralProperties()
        {
            // Arrange
            Mock<ODataResourceDeserializer> deserializer = new Mock<ODataResourceDeserializer>(_deserializerProvider);
            ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(new ODataResource { Properties = Enumerable.Empty<ODataProperty>() });
            deserializer.CallBase = true;
            deserializer.Setup(d => d.CreateResourceInstance(_productEdmType, _readContext)).Returns(42);
            deserializer.Setup(d => d.ApplyStructuralProperties(42, resourceWrapper, _productEdmType, _readContext)).Verifiable();

            // Act
            deserializer.Object.ReadResource(resourceWrapper, _productEdmType, _readContext);

            // Assert
            deserializer.Verify();
        }

        [Fact]
        public void ReadResource_Calls_ApplyNestedProperties()
        {
            // Arrange
            Mock<ODataResourceDeserializer> deserializer = new Mock<ODataResourceDeserializer>(_deserializerProvider);
            ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(new ODataResource { Properties = Enumerable.Empty<ODataProperty>() });
            deserializer.CallBase = true;
            deserializer.Setup(d => d.CreateResourceInstance(_productEdmType, _readContext)).Returns(42);
            deserializer.Setup(d => d.ApplyNestedProperties(42, resourceWrapper, _productEdmType, _readContext)).Verifiable();

            // Act
            deserializer.Object.ReadResource(resourceWrapper, _productEdmType, _readContext);

            // Assert
            deserializer.Verify();
        }

        [Fact]
        public void ReadResource_CanReadDynamicPropertiesForOpenEntityType()
        {
            // Arrange
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
            builder.EntityType<SimpleOpenCustomer>();
            builder.EnumType<SimpleEnum>();
            IEdmModel model = builder.GetEdmModel();

            IEdmEntityTypeReference customerTypeReference = model.GetEdmTypeReference(typeof(SimpleOpenCustomer)).AsEntity();

            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            ODataEnumValue enumValue = new ODataEnumValue("Third", typeof(SimpleEnum).FullName);

            ODataResource[] complexResources =
            {
                new ODataResource
                {
                    TypeName = typeof(SimpleOpenAddress).FullName,
                    Properties = new[]
                    {
                        // declared properties
                        new ODataProperty {Name = "Street", Value = "Street 1"},
                        new ODataProperty {Name = "City", Value = "City 1"},

                        // dynamic properties
                        new ODataProperty
                        {
                            Name = "DateTimeProperty",
                            Value = new DateTimeOffset(new DateTime(2014, 5, 6))
                        }
                    }
                },
                new ODataResource
                {
                    TypeName = typeof(SimpleOpenAddress).FullName,
                    Properties = new[]
                    {
                        // declared properties
                        new ODataProperty { Name = "Street", Value = "Street 2" },
                        new ODataProperty { Name = "City", Value = "City 2" },

                        // dynamic properties
                        new ODataProperty
                        {
                            Name = "ArrayProperty",
                            Value = new ODataCollectionValue { TypeName = "Collection(Edm.Int32)", Items = new[] {1, 2, 3, 4}.Cast<object>() }
                        }
                    }
                }
            };

            ODataResource odataResource = new ODataResource
            {
                Properties = new[]
                {
                    // declared properties
                    new ODataProperty { Name = "CustomerId", Value = 991 },
                    new ODataProperty { Name = "Name", Value = "Name #991" },

                    // dynamic properties
                    new ODataProperty { Name = "GuidProperty", Value = new Guid("181D3A20-B41A-489F-9F15-F91F0F6C9ECA") },
                    new ODataProperty { Name = "EnumValue", Value = enumValue },
                },
                TypeName = typeof(SimpleOpenCustomer).FullName
            };

            ODataDeserializerContext readContext = new ODataDeserializerContext()
            {
                Model = model
            };

            ODataResourceWrapper topLevelResourceWrapper = new ODataResourceWrapper(odataResource);

            ODataNestedResourceInfo resourceInfo = new ODataNestedResourceInfo
            {
                IsCollection = true,
                Name = "CollectionProperty"
            };
            ODataNestedResourceInfoWrapper resourceInfoWrapper = new ODataNestedResourceInfoWrapper(resourceInfo);
            ODataResourceSetWrapper resourceSetWrapper = new ODataResourceSetWrapper(new ODataResourceSet
            {
                TypeName = String.Format("Collection({0})", typeof(SimpleOpenAddress).FullName)
            });
            foreach (var complexResource in complexResources)
            {
                resourceSetWrapper.Resources.Add(new ODataResourceWrapper(complexResource));
            }
            resourceInfoWrapper.NestedItems.Add(resourceSetWrapper);
            topLevelResourceWrapper.NestedResourceInfos.Add(resourceInfoWrapper);

            // Act
            SimpleOpenCustomer customer = deserializer.ReadResource(topLevelResourceWrapper, customerTypeReference, readContext)
                as SimpleOpenCustomer;

            // Assert
            Assert.NotNull(customer);

            // Verify the declared properties
            Assert.Equal(991, customer.CustomerId);
            Assert.Equal("Name #991", customer.Name);

            // Verify the dynamic properties
            Assert.NotNull(customer.CustomerProperties);
            Assert.Equal(3, customer.CustomerProperties.Count());
            Assert.Equal(new Guid("181D3A20-B41A-489F-9F15-F91F0F6C9ECA"), customer.CustomerProperties["GuidProperty"]);
            Assert.Equal(SimpleEnum.Third, customer.CustomerProperties["EnumValue"]);

            // Verify the dynamic collection property
            var collectionValues = Assert.IsType<List<SimpleOpenAddress>>(customer.CustomerProperties["CollectionProperty"]);
            Assert.NotNull(collectionValues);
            Assert.Equal(2, collectionValues.Count());

            Assert.Equal(new DateTimeOffset(new DateTime(2014, 5, 6)), collectionValues[0].Properties["DateTimeProperty"]);
            Assert.Equal(new List<int> { 1, 2, 3, 4 }, collectionValues[1].Properties["ArrayProperty"]);
        }

        [Fact]
        public void ReadSource_CanReadDynamicPropertiesForInheritanceOpenEntityType()
        {
            // Arrange
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
            builder.EntityType<SimpleOpenCustomer>();
            builder.EnumType<SimpleEnum>();
            IEdmModel model = builder.GetEdmModel();

            IEdmEntityTypeReference vipCustomerTypeReference = model.GetEdmTypeReference(typeof(SimpleVipCustomer)).AsEntity();

            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            ODataResource resource = new ODataResource
            {
                Properties = new[]
                {
                    // declared properties
                    new ODataProperty { Name = "CustomerId", Value = 121 },
                    new ODataProperty { Name = "Name", Value = "VipName #121" },
                    new ODataProperty { Name = "VipNum", Value = "Vip Num 001" },

                    // dynamic properties
                    new ODataProperty { Name = "GuidProperty", Value = new Guid("181D3A20-B41A-489F-9F15-F91F0F6C9ECA") },
                },
                TypeName = typeof(SimpleVipCustomer).FullName
            };

            ODataDeserializerContext readContext = new ODataDeserializerContext()
            {
                Model = model
            };

            ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(resource);

            // Act
            SimpleVipCustomer customer = deserializer.ReadResource(resourceWrapper, vipCustomerTypeReference, readContext)
                as SimpleVipCustomer;

            // Assert
            Assert.NotNull(customer);

            // Verify the declared properties
            Assert.Equal(121, customer.CustomerId);
            Assert.Equal("VipName #121", customer.Name);
            Assert.Equal("Vip Num 001", customer.VipNum);

            // Verify the dynamic properties
            Assert.NotNull(customer.CustomerProperties);
            Assert.Single(customer.CustomerProperties);
            Assert.Equal(new Guid("181D3A20-B41A-489F-9F15-F91F0F6C9ECA"), customer.CustomerProperties["GuidProperty"]);
        }

        public class MyCustomer
        {
            public int Id { get; set; }

            [Column(TypeName = "date")]
            public DateTime Birthday { get; set; }

            [Column(TypeName = "time")]
            public TimeSpan ReleaseTime { get; set; }
        }

        [Fact]
        public void ReadResource_CanReadDatTimeRelatedProperties()
        {
            // Arrange
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
            builder.EntityType<MyCustomer>().Namespace = "NS";
            IEdmModel model = builder.GetEdmModel();

            IEdmEntityTypeReference vipCustomerTypeReference = model.GetEdmTypeReference(typeof(MyCustomer)).AsEntity();

            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            ODataResource resource = new ODataResource
            {
                Properties = new[]
                {
                    new ODataProperty { Name = "Id", Value = 121 },
                    new ODataProperty { Name = "Birthday", Value = new Date(2015, 12, 12) },
                    new ODataProperty { Name = "ReleaseTime", Value = new TimeOfDay(1, 2, 3, 4) },
                },
                TypeName = "NS.MyCustomer"
            };

            ODataDeserializerContext readContext = new ODataDeserializerContext { Model = model };
            ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(resource);

            // Act
            var customer = deserializer.ReadResource(resourceWrapper, vipCustomerTypeReference, readContext) as MyCustomer;

            // Assert
            Assert.NotNull(customer);
            Assert.Equal(121, customer.Id);
            Assert.Equal(new DateTime(2015, 12, 12), customer.Birthday);
            Assert.Equal(new TimeSpan(0, 1, 2, 3, 4), customer.ReleaseTime);
        }

        [Fact]
        public void CreateResourceInstance_ThrowsArgumentNull_ReadContext()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNull(
                () => deserializer.CreateResourceInstance(_productEdmType, readContext: null),
                "readContext");
        }

        [Fact]
        public void CreateResourceInstance_ThrowsArgument_ModelMissingFromReadContext()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act & Assert
            ExceptionAssert.ThrowsArgument(
                () => deserializer.CreateResourceInstance(_productEdmType, new ODataDeserializerContext()),
                "readContext",
                "The EDM model is missing on the read context. The model is required on the read context to deserialize the payload.");
        }

        [Fact]
        public void CreateResourceInstance_ThrowsODataException_MappingDoesNotContainEntityType()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act & Assert
            ExceptionAssert.Throws<ODataException>(
                () => deserializer.CreateResourceInstance(_productEdmType, new ODataDeserializerContext { Model = EdmCoreModel.Instance }),
                "The provided mapping does not contain a resource for the resource type 'ODataDemo.Product'.");
        }

        [Fact]
        public void CreateResourceInstance_CreatesEdmUntypedObject_IfUntypedStructured()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            IEdmStructuredTypeReference unTypedStructuredTypeRef
                = EdmUntypedStructuredTypeReference.NullableTypeReference;
            ODataDeserializerContext readContext = new ODataDeserializerContext
            {
                Model = _readContext.Model,
            };

            // Act & Assert
            Assert.IsType<EdmUntypedObject>(deserializer.CreateResourceInstance(unTypedStructuredTypeRef, readContext));
        }

        [Fact]
        public void CreateResourceInstance_CreatesDeltaOfT_IfPatchMode()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataDeserializerContext readContext = new ODataDeserializerContext
            {
                Model = _readContext.Model,
                ResourceType = typeof(Delta<Product>)
            };

            // Act & Assert
            Assert.IsType<Delta<Product>>(deserializer.CreateResourceInstance(_productEdmType, readContext));
        }

        [Fact]
        public void CreateResourceInstance_CreatesDeltaWith_ExpectedUpdatableProperties()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataDeserializerContext readContext = new ODataDeserializerContext
            {
                Model = _readContext.Model,
                ResourceType = typeof(Delta<Product>)
            };
            var structuralProperties = _readContext.Model.GetAllProperties(_productEdmType.StructuredDefinition());

            // Act
            Delta<Product> resource = deserializer.CreateResourceInstance(_productEdmType, readContext) as Delta<Product>;

            // Assert
            Assert.NotNull(resource);
            Assert.Equal(structuralProperties, resource.GetUnchangedPropertyNames());
        }

        [Fact]
        public void CreateResourceInstance_CreatesEdmEntityObject_IfTypeLessMode()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataDeserializerContext readContext = new ODataDeserializerContext
            {
                Model = _readContext.Model,
                ResourceType = typeof(IEdmObject)
            };

            // Act
            var result = deserializer.CreateResourceInstance(_productEdmType, readContext);

            // Assert
            EdmEntityObject resource = Assert.IsType<EdmEntityObject>(result);
            Assert.Equal(_productEdmType, resource.GetEdmType(), new EdmTypeReferenceEqualityComparer());
        }

        [Fact]
        public void CreateResourceInstance_CreatesEdmComplexObject_IfTypeLessMode()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataDeserializerContext readContext = new ODataDeserializerContext
            {
                Model = _readContext.Model,
                ResourceType = typeof(IEdmObject)
            };

            // Act
            var result = deserializer.CreateResourceInstance(_addressEdmType, readContext);

            // Assert
            EdmComplexObject resource = Assert.IsType<EdmComplexObject>(result);
            Assert.Equal(_addressEdmType, resource.GetEdmType(), new EdmTypeReferenceEqualityComparer());
        }

        [Fact]
        public void CreateResourceInstance_CreatesT_IfNotPatchMode()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataDeserializerContext readContext = new ODataDeserializerContext
            {
                Model = _readContext.Model,
                ResourceType = typeof(Product)
            };

            // Act & Assert
            Assert.IsType<Product>(deserializer.CreateResourceInstance(_productEdmType, readContext));
        }

        [Fact]
        public void ApplyDeletedResource_ThrowsArgumentNull_ResourceWrapper()
        {
            // Arrange & Act & Assert
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ExceptionAssert.ThrowsArgumentNull(() => deserializer.ApplyDeletedResource(null, null, null), "resourceWrapper");
        }

        [Fact]
        public void ApplyDeletedResource_ThrowsArgumentNull_ReadContext()
        {
            // Arrange & Act & Assert
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataResourceWrapper wrapper = new ODataResourceWrapper(new ODataResource());
            ExceptionAssert.ThrowsArgumentNull(() => deserializer.ApplyDeletedResource(null, wrapper, null), "readContext");
        }

        [Fact]
        public void ApplyNestedProperties_ThrowsArgumentNull_EntryWrapper()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNull(
                () => deserializer.ApplyNestedProperties(42, resourceWrapper: null, structuredType: _productEdmType, readContext: _readContext),
                "resourceWrapper");
        }

        [Fact]
        public void ApplyNestedProperties_Calls_ApplyNavigationPropertyForEachNavigationLink()
        {
            // Arrange
            ODataResourceWrapper resource = new ODataResourceWrapper(new ODataResource());
            resource.NestedResourceInfos.Add(new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo()));
            resource.NestedResourceInfos.Add(new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo()));

            Mock<ODataResourceDeserializer> deserializer = new Mock<ODataResourceDeserializer>(_deserializerProvider);
            deserializer.CallBase = true;
            deserializer.Setup(d => d.ApplyNestedProperty(42, resource.NestedResourceInfos[0], _productEdmType, _readContext)).Verifiable();
            deserializer.Setup(d => d.ApplyNestedProperty(42, resource.NestedResourceInfos[1], _productEdmType, _readContext)).Verifiable();

            // Act
            deserializer.Object.ApplyNestedProperties(42, resource, _productEdmType, _readContext);

            // Assert
            deserializer.Verify();
        }

        [Fact]
        public void ApplyNestedProperty_ThrowsArgumentNull_ResourceInfoWrapper()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNull(
                () => deserializer.ApplyNestedProperty(42, resourceInfoWrapper: null, structuredType: _productEdmType,
                    readContext: _readContext),
                "resourceInfoWrapper");
        }

        [Fact]
        public void ApplyNestedProperty_ThrowsArgumentNull_EntityResource()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataNestedResourceInfoWrapper resourceInfoWrapper = new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo());

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNull(
                () => deserializer.ApplyNestedProperty(resource: null, resourceInfoWrapper: resourceInfoWrapper,
                    structuredType: _productEdmType, readContext: _readContext),
                "resource");
        }

        [Fact]
        public void ApplyNestedProperty_ThrowsODataException_NavigationPropertyNotfound()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ODataNestedResourceInfoWrapper resourceInfoWrapper = new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo { Name = "SomeProperty" });

            // Act & Assert
            ExceptionAssert.Throws<ODataException>(
                () => deserializer.ApplyNestedProperty(42, resourceInfoWrapper, _productEdmType, _readContext),
                "Cannot find nested property 'SomeProperty' on the resource type 'ODataDemo.Product'.");
        }

        [Fact]
        public void ApplyNestedProperty_UsesThePropertyAlias_ForResourceSet()
        {
            // Arrange
            CustomersModelWithInheritance model = new CustomersModelWithInheritance();
            model.Model.SetAnnotationValue(model.Customer, new ClrTypeAnnotation(typeof(Customer)));
            model.Model.SetAnnotationValue(model.Order, new ClrTypeAnnotation(typeof(Order)));
            model.Model.SetAnnotationValue(
                model.Customer.FindProperty("Orders"),
                new ClrPropertyInfoAnnotation(typeof(Customer).GetProperty("AliasedOrders")));
            ODataResourceSetWrapper resourceSetWrapper = new ODataResourceSetWrapper(new ODataResourceSet());
            resourceSetWrapper.Resources.Add(new ODataResourceWrapper(
                new ODataResource { Properties = new[] { new ODataProperty { Name = "ID", Value = 42 } } }));

            Customer customer = new Customer();
            ODataNestedResourceInfoWrapper resourceInfoWrapper =
                new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo { Name = "Orders" });
            resourceInfoWrapper.NestedItems.Add(resourceSetWrapper);

            ODataDeserializerContext context = new ODataDeserializerContext { Model = model.Model };

            // Act
            new ODataResourceDeserializer(_deserializerProvider)
                .ApplyNestedProperty(customer, resourceInfoWrapper, model.Customer.AsReference(), context);

            // Assert
            Assert.Single(customer.AliasedOrders);
            Assert.Equal(42, customer.AliasedOrders[0].ID);
        }

        [Fact]
        public void ApplyNestedProperty_UsesThePropertyAlias_ForResourceWrapper()
        {
            // Arrange
            CustomersModelWithInheritance model = new CustomersModelWithInheritance();
            model.Model.SetAnnotationValue(model.Customer, new ClrTypeAnnotation(typeof(Customer)));
            model.Model.SetAnnotationValue(model.Order, new ClrTypeAnnotation(typeof(Order)));
            model.Model.SetAnnotationValue(
                model.Order.FindProperty("Customer"),
                new ClrPropertyInfoAnnotation(typeof(Order).GetProperty("AliasedCustomer")));
            ODataResource resource = new ODataResource { Properties = new[] { new ODataProperty { Name = "ID", Value = 42 } } };

            Order order = new Order();
            ODataNestedResourceInfoWrapper resourceInfoWrapper =
                new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo { Name = "Customer" });
            resourceInfoWrapper.NestedItems.Add(new ODataResourceWrapper(resource));

            ODataDeserializerContext context = new ODataDeserializerContext { Model = model.Model };

            // Act
            new ODataResourceDeserializer(_deserializerProvider)
                .ApplyNestedProperty(order, resourceInfoWrapper, model.Order.AsReference(), context);

            // Assert
            Assert.Equal(42, order.AliasedCustomer.ID);
        }

        [Fact]
        public void ApplyNestedProperty_Works_ForEntityReferenceLinkWrapper()
        {
            // Arrange
            Product product = new Product();
            ODataNestedResourceInfoWrapper resourceInfoWrapper =
                new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo { Name = "Supplier" });
            resourceInfoWrapper.NestedItems.Add(
                new ODataEntityReferenceLinkWrapper(
                    new ODataEntityReferenceLink
                    {
                        Url = new Uri("http://localhost/Suppliers(42)", UriKind.RelativeOrAbsolute)
                    })
                );

            HttpRequest request = RequestFactory.Create(HttpMethods.Get, "http://localhost");
            ODataDeserializerContext context = new ODataDeserializerContext
            {
                Model = _edmModel,
                Request = request
            };

            // Act
            new ODataResourceDeserializer(_deserializerProvider)
                .ApplyNestedProperty(product, resourceInfoWrapper, _productEdmType, context);

            // Assert
            Assert.Equal(42, product.Supplier.ID);
        }

        [Fact]
        public void ApplyNestedProperty_Works_ForEntityReferenceLinksWrapper()
        {
            // Arrange
            Supplier supplier = new Supplier();
            ODataNestedResourceInfoWrapper resourceInfoWrapper =
                new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo { Name = "Products" });
            resourceInfoWrapper.NestedItems.Add(
                new ODataEntityReferenceLinkWrapper(
                    new ODataEntityReferenceLink
                    {
                        Url = new Uri("http://localhost/Products(5)", UriKind.RelativeOrAbsolute)
                    })
                );
            resourceInfoWrapper.NestedItems.Add(
                new ODataEntityReferenceLinkWrapper(
                    new ODataEntityReferenceLink
                    {
                        Url = new Uri("http://localhost/Products(6)", UriKind.RelativeOrAbsolute)
                    })
                );

            HttpRequest request = RequestFactory.Create(HttpMethods.Get, "http://localhost");
            ODataDeserializerContext context = new ODataDeserializerContext
            {
                Model = _edmModel,
                Request = request
            };

            // Act
            new ODataResourceDeserializer(_deserializerProvider)
                .ApplyNestedProperty(supplier, resourceInfoWrapper, _supplierEdmType, context);

            // Assert
            Assert.Equal(2, supplier.Products.Count);
            Assert.Collection(supplier.Products,
                e => Assert.Equal(5, e.ID),
                e => Assert.Equal(6, e.ID));
        }

        #region Untyped_NestedResourceInfo
        public class Person
        {
            public int Id { get; set; }

            public object Data { get; set; }

            public IList<object> Sources { get; set; }

            public IDictionary<string, object> Dynamics { get; set; }
        }

        private static IEdmModel GetUntypedModel()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Person>("People");
            return builder.GetEdmModel();
        }

        private static IEdmModel EdmUntypedModel = GetUntypedModel();

        [Fact]
        public void ReadResource_Works_ForDeclaredUntypedProperty_Primitive()
        {
            // Arrange
            Func<ODataResourceWrapper> arranges = () =>
            {
                return new ODataResourceWrapper(new ODataResource
                {
                    TypeName = "Microsoft.AspNetCore.OData.Tests.Formatter.Deserialization.Person",
                    Properties = new ODataProperty[]
                    {
                        new ODataProperty { Name = "Data", Value = 42 },
                        new ODataProperty { Name = "DynamicProperty", Value = 12 }
                    }
                });
            };

            // Assert
            Action<object> asserts = (s) =>
            {
                Assert.NotNull(s);
                Person p = Assert.IsType<Person>(s);
                Assert.Equal(42, p.Data);
                Assert.Equal(12, p.Dynamics["DynamicProperty"]);
            };

            RunReadResourceUntypedTestAndVerify(arranges, asserts);
        }

        private void RunReadResourceUntypedTestAndVerify(Func<ODataResourceWrapper> arranges, Action<object> asserts)
        {
            // Arrange
            IEdmModel model = EdmUntypedModel;
            var personEdmType = model.GetEdmTypeReference(typeof(Person)).AsEntity();

            ODataResourceWrapper resourceWrapper = arranges();

            HttpRequest request = RequestFactory.Create(HttpMethods.Get, "http://localhost");
            ODataDeserializerContext context = new ODataDeserializerContext
            {
                Model = model,
                Request = request
            };

            // Act
            object source = new ODataResourceDeserializer(_deserializerProvider)
                .ReadResource(resourceWrapper, personEdmType, context);

            // Assert
            asserts(source);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ApplyNestedProperty_Works_ForDeclaredOrUnDeclaredUntypedProperty_NestedResource(bool isDeclared)
        {
            // Arrange
            /*
            {
              "Data": {
                   ......
               }
            }
            */
            Person aPerson = new Person();
            Assert.Null(aPerson.Data);

            Func<ODataNestedResourceInfoWrapper> arranges = () =>
            {
                string propertyName = isDeclared ? "Data" : "AnyDynamicPropertyName";
                ODataNestedResourceInfoWrapper nestedResourceInfo =
                    new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo { Name = propertyName });
                nestedResourceInfo.NestedItems.Add(
                    new ODataResourceWrapper(
                        new ODataResource { Properties = new[] { new ODataProperty { Name = "NestedID", Value = 42 } } }));
                return nestedResourceInfo;
            };

            // Assert
            Action asserts = () =>
            {
                EdmUntypedObject unTypedData;
                if (isDeclared)
                {
                    unTypedData = Assert.IsType<EdmUntypedObject>(aPerson.Data);
                }
                else
                {
                    unTypedData = Assert.IsType<EdmUntypedObject>(aPerson.Dynamics["AnyDynamicPropertyName"]);
                }

                KeyValuePair<string, object> property = Assert.Single(unTypedData);
                Assert.Equal("NestedID", property.Key);
                Assert.Equal(42, property.Value);
            };

            RunUntypedTestAndVerify(aPerson, arranges, asserts);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ApplyNestedProperty_Works_ForDeclaredOrUndelcaredUntypedProperty_NestedCollection(bool isDeclared)
        {
            // Arrange
            /*
            {
              "Data": [
                  13,
                  { ... },
                  null,
                  { ... }
               ]
            }
            */
            Person aPerson = new Person();
            Assert.Null(aPerson.Data);

            Func<ODataNestedResourceInfoWrapper> arranges = () =>
            {
                ODataResourceSetWrapper setWrapper = new ODataResourceSetWrapper(new ODataResourceSet());
                setWrapper.Items.Add(new ODataPrimitiveWrapper(new ODataPrimitiveValue(13)));

                ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(
                    new ODataResource { Properties = new[] { new ODataProperty { Name = "Data_ID", Value = 42 } } });
                setWrapper.Items.Add(resourceWrapper);
                setWrapper.Items.Add(null);

                // Explicitly added into 'Resources' to test.
                setWrapper.Resources.Add(resourceWrapper);
                setWrapper.Resources.Add(new ODataResourceWrapper(
                        new ODataResource { Properties = new[] { new ODataProperty { Name = "Extra_ID", Value = "42" } } }));

                string propertyName = isDeclared ? "Data" : "AnyDynamicPropertyName";
                ODataNestedResourceInfoWrapper nestedResourceInfo =
                    new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo { Name = propertyName, IsCollection = true });
                nestedResourceInfo.NestedItems.Add(setWrapper);
                return nestedResourceInfo;
            };

            // Assert
            Action asserts = () =>
            {
                EdmUntypedCollection unTypedData;
                if (isDeclared)
                {
                    unTypedData = Assert.IsType<EdmUntypedCollection>(aPerson.Data);
                }
                else
                {
                    unTypedData = Assert.IsType<EdmUntypedCollection>(aPerson.Dynamics["AnyDynamicPropertyName"]);
                }

                Assert.Equal(4, unTypedData.Count);
                Assert.Collection(unTypedData,
                    e =>
                    {
                        Assert.Equal(13, e);
                    },
                    e =>
                    {
                        EdmUntypedObject unTypedObj = Assert.IsType<EdmUntypedObject>(e);
                        KeyValuePair<string, object> singleProperty = Assert.Single(unTypedObj);
                        Assert.Equal("Data_ID", singleProperty.Key);
                        Assert.Equal(42, singleProperty.Value);
                    },
                    e => Assert.Null(e),
                    e =>
                    {
                        EdmUntypedObject unTypedObj = Assert.IsType<EdmUntypedObject>(e);
                        KeyValuePair<string, object> singleProperty = Assert.Single(unTypedObj);
                        Assert.Equal("Extra_ID", singleProperty.Key);
                        Assert.Equal("42", singleProperty.Value);
                    });
            };

            RunUntypedTestAndVerify(aPerson, arranges, asserts);
        }

        [Theory]
        [InlineData("Data")] // ==> declared Edm.Untyped property
        [InlineData("Sources")]  // ==> declared Collection(Edm.Untyped) property
        [InlineData("AnyDynamicPropertyName")] // ==> un-declared (or dynamic) property
        public void ApplyNestedProperty_Works_ForDeclaredOrUndelcaredUntypedProperty_NestedCollectionofCollection(string propertyName)
        {
            // Arrange
            /*
            {
              "Data": [
                  [
                     {
                        "Aws/Name": [
                           [ true, 15]
                        ]
                     }
                  ],
                  {
                     "Aws/Name": [
                       [ true, 15]
                     ]
                  }
               ]
            }
            */
            Person aPerson = new Person();
            Assert.Null(aPerson.Data);

            Func<ODataNestedResourceInfoWrapper> arranges = () =>
            {
                ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(new ODataResource
                {
                    Properties = Enumerable.Empty<ODataProperty>()
                });

                ODataNestedResourceInfoWrapper awsNameNestedResourceInfo = new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo
                {
                    Name = "Aws/Name" // explicitly use '/' in the name.
                });
                resourceWrapper.NestedResourceInfos.Add(awsNameNestedResourceInfo);

                ODataResourceSetWrapper setOfAwsNameWrapper = new ODataResourceSetWrapper(new ODataResourceSet());
                awsNameNestedResourceInfo.NestedItems.Add(setOfAwsNameWrapper);

                ODataResourceSetWrapper setOfSetAwsNameWrapper = new ODataResourceSetWrapper(new ODataResourceSet());
                setOfSetAwsNameWrapper.Items.Add(new ODataPrimitiveWrapper(new ODataPrimitiveValue(true)));
                setOfSetAwsNameWrapper.Items.Add(new ODataPrimitiveWrapper(new ODataPrimitiveValue(15)));
                setOfAwsNameWrapper.Items.Add(setOfSetAwsNameWrapper);

                // first item in 'Data' collection
                ODataResourceSetWrapper setWrapperOfDataSet = new ODataResourceSetWrapper(new ODataResourceSet());
                setWrapperOfDataSet.Items.Add(resourceWrapper);

                // Data collection
                ODataResourceSetWrapper setWrapper = new ODataResourceSetWrapper(new ODataResourceSet());
                setWrapper.Items.Add(setWrapperOfDataSet);
                setWrapper.Items.Add(resourceWrapper);

                ODataNestedResourceInfoWrapper nestedResourceInfo =
                    new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo { Name = propertyName, IsCollection = true });
                nestedResourceInfo.NestedItems.Add(setWrapper);
                return nestedResourceInfo;
            };

            // Assert
            Action<EdmUntypedObject> verifyResource = o =>
            {
                KeyValuePair<string, object> singleProperty = Assert.Single(o);
                Assert.Equal("Aws/Name", singleProperty.Key);
                EdmUntypedCollection aswNameValuecol = Assert.IsType<EdmUntypedCollection>(singleProperty.Value);
                EdmUntypedCollection colInAwsNameCol = Assert.IsType<EdmUntypedCollection>(Assert.Single(aswNameValuecol));
                Assert.Equal(2, colInAwsNameCol.Count);
                Assert.Collection(colInAwsNameCol,
                    e => Assert.True((bool)e),
                    e => Assert.Equal(15, e));
            };

            Action asserts = () =>
            {
                IList<object> unTypedData;
                if (propertyName == "Data")
                {
                    Assert.IsType<EdmUntypedCollection>(aPerson.Data);
                    unTypedData = Assert.IsAssignableFrom<IList<object>>(aPerson.Data);
                }
                else if (propertyName == "Sources")
                {
                    unTypedData = aPerson.Sources;
                }
                else
                {
                    EdmUntypedCollection data = Assert.IsType<EdmUntypedCollection>(aPerson.Dynamics["AnyDynamicPropertyName"]);
                    unTypedData = Assert.IsAssignableFrom<IList<object>>(data);
                }

                Assert.Equal(2, unTypedData.Count);
                Assert.Collection(unTypedData,
                    e =>
                    {
                        EdmUntypedCollection nestedCollection = Assert.IsType<EdmUntypedCollection>(e);
                        EdmUntypedObject nestedObjInNestedCollection = Assert.IsType<EdmUntypedObject>(Assert.Single(nestedCollection));

                        verifyResource(nestedObjInNestedCollection);
                    },
                    e =>
                    {
                        EdmUntypedObject unTypedObj = Assert.IsType<EdmUntypedObject>(e);
                        verifyResource(unTypedObj);
                    });
            };

            RunUntypedTestAndVerify(aPerson, arranges, asserts);
        }

        private void RunUntypedTestAndVerify(object source, Func<ODataNestedResourceInfoWrapper> arranges, Action asserts)
        {
            // Arrange
            IEdmModel model = EdmUntypedModel;
            var personEdmType = model.GetEdmTypeReference(typeof(Person)).AsEntity();

            ODataNestedResourceInfoWrapper resourceInfoWrapper = arranges();

            HttpRequest request = RequestFactory.Create(HttpMethods.Get, "http://localhost");
            ODataDeserializerContext context = new ODataDeserializerContext
            {
                Model = model,
                Request = request
            };

            // Act
            new ODataResourceDeserializer(_deserializerProvider)
                .ApplyNestedProperty(source, resourceInfoWrapper, personEdmType, context);

            // Assert
            asserts();
        }
        #endregion

        [Fact(Skip = "This test need to refactor and the ODataResourceDeserializer has the problem for nested resource set.")]
        public void ApplyNestedProperty_Works_ForDeltaResourceSetWrapper()
        {
            // Arrange
            Delta<Supplier> supplier = new Delta<Supplier>();
            ODataNestedResourceInfoWrapper resourceInfoWrapper =
                new ODataNestedResourceInfoWrapper(new ODataNestedResourceInfo { Name = "Products" });

            ODataDeltaResourceSetWrapper wrapper = new ODataDeltaResourceSetWrapper(new ODataDeltaResourceSet());
            ODataResource resource = new ODataResource { Properties = new[] { new ODataProperty { Name = "ID", Value = 42 } } };
            wrapper.DeltaItems.Add(new ODataResourceWrapper(resource));

            resourceInfoWrapper.NestedItems.Add(wrapper);
            HttpRequest request = RequestFactory.Create(HttpMethods.Get, "http://localhost");
            ODataDeserializerContext context = new ODataDeserializerContext
            {
                Model = _edmModel,
                Request = request
            };

            // Act
            new ODataResourceDeserializer(_deserializerProvider)
                .ApplyNestedProperty(supplier, resourceInfoWrapper, _supplierEdmType, context);

            // Assert
            Assert.True(supplier.TryGetPropertyValue("Products", out _));
        }

        [Fact]
        public void ApplyStructuralProperties_ThrowsArgumentNull_resourceWrapper()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNull(
                () => deserializer.ApplyStructuralProperties(42, resourceWrapper: null, structuredType: _productEdmType, readContext: _readContext),
                "resourceWrapper");
        }

        [Fact]
        public void ApplyStructuralProperties_Calls_ApplyStructuralPropertyOnEachPropertyInResource()
        {
            // Arrange
            var deserializer = new Mock<ODataResourceDeserializer>(_deserializerProvider);
            ODataProperty[] properties = new[] { new ODataProperty(), new ODataProperty() };
            ODataResourceWrapper resourceWrapper = new ODataResourceWrapper(new ODataResource { Properties = properties });

            deserializer.CallBase = true;
            deserializer.Setup(d => d.ApplyStructuralProperty(42, properties[0], _productEdmType, _readContext)).Verifiable();
            deserializer.Setup(d => d.ApplyStructuralProperty(42, properties[1], _productEdmType, _readContext)).Verifiable();

            // Act
            deserializer.Object.ApplyStructuralProperties(42, resourceWrapper, _productEdmType, _readContext);

            // Assert
            deserializer.Verify();
        }

        [Fact]
        public void ApplyStructuralProperty_ThrowsArgumentNull_Resource()
        {
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ExceptionAssert.ThrowsArgumentNull(
                () => deserializer.ApplyStructuralProperty(resource: null, structuralProperty: new ODataProperty(),
                    structuredType: _productEdmType, readContext: _readContext),
                "resource");
        }

        [Fact]
        public void ApplyStructuralProperty_ThrowsArgumentNull_StructuralProperty()
        {
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            ExceptionAssert.ThrowsArgumentNull(
                () => deserializer.ApplyStructuralProperty(42, structuralProperty: null, structuredType: _productEdmType, readContext: _readContext),
                "structuralProperty");
        }

        [Fact]
        public void ApplyStructuralProperty_SetsProperty()
        {
            // Arrange
            var deserializer = new ODataResourceDeserializer(_deserializerProvider);
            Product product = new Product();
            ODataProperty property = new ODataProperty { Name = "ID", Value = 42 };

            // Act
            deserializer.ApplyStructuralProperty(product, property, _productEdmType, _readContext);

            // Assert
            Assert.Equal(42, product.ID);
        }

        [Fact]
        public async Task ReadFromStreamAsync()
        {
            // Arrange
            string content = @"{
                ""ID"":0,
                ""Name"":""Bread"",
                ""Description"":""Whole grain bread"",
                ""ReleaseDate"":""1992-01-01T00:00:00Z"",
                ""PublishDate"":""1997-07-01"",
                ""DiscontinuedDate"":null,
                ""Rating"":4,
                ""Price"":2.5
            }";
            ODataResourceDeserializer deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act
            Product product = await deserializer.ReadAsync(GetODataMessageReader(content, _edmModel),
                typeof(Product), _readContext) as Product;

            // Assert
            Assert.Equal(0, product.ID);
            Assert.Equal(4, product.Rating);
            Assert.Equal(2.5m, product.Price);
            Assert.Equal(product.ReleaseDate, new DateTimeOffset(new DateTime(1992, 1, 1, 0, 0, 0), TimeSpan.Zero));
            Assert.Equal(product.PublishDate, new Date(1997, 7, 1));
            Assert.Null(product.DiscontinuedDate);
        }

        [Fact]
        public async Task ReadFromStreamAsync_ComplexTypeAndInlineData()
        {
            // Arrange
            string content = _supplyRequestResource;
            ODataResourceDeserializer deserializer = new ODataResourceDeserializer(_deserializerProvider);

            var readContext = new ODataDeserializerContext
            {
                Path = new ODataPath(new EntitySetSegment(_edmModel.EntityContainer.FindEntitySet("Suppliers"))),
                Model = _edmModel,
                ResourceType = typeof(Supplier)
            };

            // Act
            Supplier supplier = await deserializer.ReadAsync(GetODataMessageReader(content, _edmModel),
                typeof(Supplier), readContext) as Supplier;

            // Assert
            Assert.Equal("Supplier Name", supplier.Name);

            Assert.NotNull(supplier.Products);
            Assert.Equal(6, supplier.Products.Count);
            Assert.Equal("soda", supplier.Products.ToList()[1].Name);

            Assert.NotNull(supplier.Address);
            Assert.Equal("Supplier City", supplier.Address.City);
            Assert.Equal("123456", supplier.Address.ZipCode);
        }

        [Fact]
        public async Task ReadAsync_PatchMode()
        {
            // Arrange
            string content = @"{
                ""ID"":123,
                ""Name"":""Supplier Name"",
                ""Address"":
                {
                    ""Street"":""Supplier Street"",
                    ""City"":""Supplier City"",
                    ""State"":""WA"",
                    ""ZipCode"":""123456"",
                    ""CountryOrRegion"":""USA""
                }
            }";

            var readContext = new ODataDeserializerContext
            {
                Path = new ODataPath(new EntitySetSegment(_edmModel.EntityContainer.FindEntitySet("Suppliers"))),
                Model = _edmModel,
                ResourceType = typeof(Delta<Supplier>)
            };

            ODataResourceDeserializer deserializer =
                new ODataResourceDeserializer(_deserializerProvider);

            // Act
            Delta<Supplier> supplier = await deserializer.ReadAsync(GetODataMessageReader(content, _edmModel),
                typeof(Delta<Supplier>), readContext) as Delta<Supplier>;

            // Assert
            Assert.NotNull(supplier);
            Assert.Equal(supplier.GetChangedPropertyNames(), new string[] { "ID", "Name", "Address" });

            Assert.Equal("Supplier Name", (supplier as dynamic).Name);
            Assert.Equal("Supplier City", (supplier as dynamic).Address.City);
            Assert.Equal("123456", (supplier as dynamic).Address.ZipCode);
        }

        [Fact]
        public void ReadAsync_ThrowsOnUnknownEntityType()
        {
            // Arrange
            string content = _supplyRequestResource;
            ODataResourceDeserializer deserializer = new ODataResourceDeserializer(_deserializerProvider);

            // Act & Assert
            ExceptionAssert.Throws<ODataException>(() => deserializer.ReadAsync(GetODataMessageReader(content, _edmModel),
                typeof(Product), _readContext).Wait(), "The property 'Concurrency' does not exist on type 'ODataDemo.Product'. Make sure to only use property names that are defined by the type or mark the type as open type.");
        }

        private static ODataMessageReader GetODataMessageReader(IODataRequestMessage oDataRequestMessage, IEdmModel edmModel)
        {
            return new ODataMessageReader(oDataRequestMessage, new ODataMessageReaderSettings(), edmModel);
        }

        private static ODataMessageReader GetODataMessageReader(string content, IEdmModel edmModel)
        {
            IODataRequestMessage oDataRequestMessage = GetODataMessage(content, edmModel);
            return new ODataMessageReader(oDataRequestMessage, new ODataMessageReaderSettings(), edmModel);
        }

        private static IODataRequestMessage GetODataMessage(string content, IEdmModel model)
        {
            //    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/OData/OData.svc/Products");

            HttpRequest request = RequestFactory.Create("Post", "http://localhost/odata/Products", opt => opt.AddRouteComponents("odata", model));

            //request.Content = new StringContent(content);
            //request.Headers.Add("OData-Version", "4.0");

            //MediaTypeWithQualityHeaderValue mediaType = new MediaTypeWithQualityHeaderValue("application/json");
            //mediaType.Parameters.Add(new NameValueHeaderValue("odata.metadata", "full"));
            //request.Headers.Accept.Add(mediaType);
            //request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            request.Body = new MemoryStream(contentBytes);
            request.ContentType = "application/json";
            request.ContentLength = contentBytes.Length;
            request.Headers.Add("OData-Version", "4.0");
            request.Headers.Add("Accept", "application/json;odata.metadata=full");

            return new HttpRequestODataMessage(request);
        }

        private static IEdmModel GetEdmModel()
        {
            string csdl = @"<?xml version=""1.0"" encoding=""utf-8""?>
<edmx:Edmx Version=""4.0""
    xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
  <edmx:DataServices m:DataServiceVersion=""4.0"" m:MaxDataServiceVersion=""4.0""
      xmlns:m=""http://docs.oasis-open.org/odata/ns/metadata"">
    <Schema Namespace=""ODataDemo""
        xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
      <EntityType Name=""Product"">
        <Key>
          <PropertyRef Name=""ID"" />
        </Key>
        <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""Name"" Type=""Edm.String"" />
        <Property Name=""Description"" Type=""Edm.String"" />
        <Property Name=""ReleaseDate"" Type=""Edm.DateTimeOffset"" Nullable=""false"" />
        <Property Name=""DiscontinuedDate"" Type=""Edm.DateTimeOffset"" />
        <Property Name=""PublishDate"" Type=""Edm.Date"" />
        <Property Name=""Rating"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""Price"" Type=""Edm.Decimal"" Nullable=""false"" />
        <NavigationProperty Name=""Category"" Type=""ODataDemo.Category"" Partner=""Products"" />
        <NavigationProperty Name=""Supplier"" Type=""ODataDemo.Supplier"" Partner=""Products"" />
      </EntityType>
      <EntityType Name=""Category"">
        <Key>
          <PropertyRef Name=""ID"" />
        </Key>
        <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""Name"" Type=""Edm.String"" />
        <NavigationProperty Name=""Products"" Type=""Collection(ODataDemo.Product)"" Partner=""Category"" />
      </EntityType>
      <EntityType Name=""Supplier"">
        <Key>
          <PropertyRef Name=""ID"" />
        </Key>
        <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""Name"" Type=""Edm.String"" />
        <Property Name=""Address"" Type=""ODataDemo.Address"" />
        <Property Name=""Location"" Type=""Edm.GeographyPoint"" SRID=""Variable"" />
        <Property Name=""Concurrency"" Type=""Edm.Int32"" Nullable=""false"" />
        <NavigationProperty Name=""Products"" Type=""Collection(ODataDemo.Product)"" Partner=""Supplier"" />
      </EntityType>
      <ComplexType Name=""Address"">
        <Property Name=""Street"" Type=""Edm.String"" />
        <Property Name=""City"" Type=""Edm.String"" />
        <Property Name=""State"" Type=""Edm.String"" />
        <Property Name=""ZipCode"" Type=""Edm.String"" />
        <Property Name=""CountryOrRegion"" Type=""Edm.String"" />
      </ComplexType>
      <Function Name=""GetProductsByRating"" m:HttpMethod=""GET"">
        <ReturnType Type=""Collection(ODataDemo.Product)"" />
        <Parameter Name=""rating"" Type=""Edm.Int32"" Nullable=""false"" />
      </Function>
      <EntityContainer Name=""DemoService"" m:IsDefaultEntityContainer=""true"">
        <EntitySet Name=""Products"" EntityType=""ODataDemo.Product"">
          <NavigationPropertyBinding Path=""Category"" Target=""Categories"" />
          <NavigationPropertyBinding Path=""Supplier"" Target=""Suppliers"" />
        </EntitySet>
        <EntitySet Name=""Categories"" EntityType=""ODataDemo.Category"">
          <NavigationPropertyBinding Path=""Products"" Target=""Products"" />
        </EntitySet>
        <EntitySet Name=""Suppliers"" EntityType=""ODataDemo.Supplier"">
          <NavigationPropertyBinding Path=""Products"" Target=""Products"" />
          <Annotation Term=""Org.OData.Core.V1.OptimisticConcurrency"">
            <Collection>
              <PropertyPath>Concurrency</PropertyPath>
            </Collection>
          </Annotation>
        </EntitySet>
        <FunctionImport Name=""GetProductsByRating"" Function=""ODataDemo.GetProductsByRating"" EntitySet=""Products"" IncludeInServiceDocument=""true"" />
      </EntityContainer>
    </Schema>
  </edmx:DataServices>
</edmx:Edmx>";

            IEdmModel edmModel;
            IEnumerable<EdmError> edmErrors;
            Assert.True(CsdlReader.TryParse(XmlReader.Create(new StringReader(csdl)), out edmModel, out edmErrors));

            edmModel.SetAnnotationValue<ClrTypeAnnotation>(edmModel.FindDeclaredType("ODataDemo.Product"), new ClrTypeAnnotation(typeof(Product)));
            edmModel.SetAnnotationValue<ClrTypeAnnotation>(edmModel.FindDeclaredType("ODataDemo.Supplier"), new ClrTypeAnnotation(typeof(Supplier)));
            edmModel.SetAnnotationValue<ClrTypeAnnotation>(edmModel.FindDeclaredType("ODataDemo.Address"), new ClrTypeAnnotation(typeof(Address)));
            edmModel.SetAnnotationValue<ClrTypeAnnotation>(edmModel.FindDeclaredType("ODataDemo.Category"), new ClrTypeAnnotation(typeof(Category)));

            return edmModel;
        }

        public abstract class BaseType
        {
            public int ID { get; set; }
        }

        public class Product
        {
            public int ID { get; set; }

            public string Name { get; set; }

            public string Description { get; set; }

            public DateTimeOffset? ReleaseDate { get; set; }

            public DateTimeOffset? DiscontinuedDate { get; set; }

            public Date PublishDate { get; set; }

            public int Rating { get; set; }

            public decimal Price { get; set; }

            public virtual Category Category { get; set; }

            public virtual Supplier Supplier { get; set; }
        }

        public class Category
        {
            public int ID { get; set; }

            public string Name { get; set; }

            public virtual IList<Product> Products { get; set; }
        }

        public class Supplier
        {
            public int ID { get; set; }

            public string Name { get; set; }

            public Address Address { get; set; }

            public int Concurrency { get; set; }

            public SupplierRating SupplierRating { get; set; }

            public virtual IList<Product> Products { get; set; }
        }

        public class Address
        {
            public string Street { get; set; }

            public string City { get; set; }

            public string State { get; set; }

            public string ZipCode { get; set; }

            public string CountryOrRegion { get; set; }
        }

        public enum SupplierRating
        {
            Gold,
            Silver,
            Bronze
        }

        private class Customer
        {
            public int ID { get; set; }

            public Order[] AliasedOrders { get; set; }
        }

        private class Order
        {
            public int ID { get; set; }

            public Customer AliasedCustomer { get; set; }
        }
    }
}
