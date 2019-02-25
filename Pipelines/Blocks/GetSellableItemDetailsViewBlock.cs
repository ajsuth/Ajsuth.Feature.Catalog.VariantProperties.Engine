// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GetSellableItemDetailsViewBlock.cs" company="Sitecore Corporation">
//   Copyright (c) Sitecore Corporation 1999-2019
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Ajsuth.Feature.Catalog.VariantProperties.Engine.Pipelines.Blocks
{
    using Sitecore.Commerce.Core;
    using Sitecore.Commerce.EntityViews;
    using Sitecore.Commerce.Plugin.Catalog;
    using Sitecore.Framework.Conditions;
    using Sitecore.Framework.Pipelines;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the get sellable item details pipeline block
    /// </summary>
    /// <seealso>
    ///     <cref>
    ///         Sitecore.Framework.Pipelines.PipelineBlock{Sitecore.Commerce.EntityViews.EntityView,
    ///         Sitecore.Commerce.EntityViews.EntityView, Sitecore.Commerce.Core.CommercePipelineExecutionContext}
    ///     </cref>
    /// </seealso>
    [PipelineDisplayName(VariantPropertiesConstants.Pipelines.Blocks.GetSellableItemDetailsView)]
    public class GetSellableItemDetailsViewBlock : PipelineBlock<EntityView, EntityView, CommercePipelineExecutionContext>
    {
		/// <summary>
		/// Gets or sets the commander.
		/// </summary>
		/// <value>The commander.</value>
		protected CommerceCommander Commander { get; set; }

        /// <inheritdoc />
        /// <summary>Initializes a new instance of the <see cref="T:Sitecore.Framework.Pipelines.PipelineBlock" /> class.</summary>
        /// <param name="commander">The commerce commander.</param>
        public GetSellableItemDetailsViewBlock(CommerceCommander commander)
		    : base(null)
		{
            this.Commander = commander;
        }

        /// <summary>
        /// The execute.
        /// </summary>
        /// <param name="arg">The pipeline argument.</param>
        /// <param name="context">The context.</param>
        /// <returns>The <see cref="PipelineArgument"/>.</returns>
        public override Task<EntityView> Run(EntityView entityView, CommercePipelineExecutionContext context)
        {
            Condition.Requires(entityView).IsNotNull((Name) + ": The argument cannot be null");

            var policy = context.GetPolicy<KnownCatalogViewsPolicy>();
            var request = context.CommerceContext.GetObject<EntityViewArgument>();
            if (string.IsNullOrEmpty(request?.ViewName)
                    || !request.ViewName.Equals(policy.Master, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(entityView);
            }
            
            if (!(request.Entity is SellableItem) || !string.IsNullOrEmpty(request.ForAction))
            {
                return Task.FromResult(entityView);
            }

            var isBundle = request.Entity != null && request.Entity.HasComponent<BundleComponent>();
            if (isBundle)
            {
                return Task.FromResult(entityView);
            }

            var variantsView = entityView.ChildViews.FirstOrDefault(c => c.Name == policy.SellableItemVariants) as EntityView;
            UpdateVariantsView(variantsView, request.Entity, context);
            
            return Task.FromResult(entityView);
        }
        
        /// <summary>
        /// Updates variants entity view
        /// </summary>
        /// <param name="variantsView">The variants view.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="context">The context.</param>
        protected virtual void UpdateVariantsView(EntityView variantsView, CommerceEntity entity, CommercePipelineExecutionContext context)
        {
            var variations = entity.GetComponent<ItemVariationsComponent>().ChildComponents.OfType<ItemVariationComponent>().ToList();
            if (variations != null && variations.Count <= 0)
            {
                return;
            }

            var variationPropertyPolicy = context.CommerceContext.Environment.GetPolicy<VariationPropertyPolicy>();
            foreach (var variation in variations)
            {
                var variationView = variantsView.ChildViews.FirstOrDefault(c => ((EntityView)c).ItemId == variation.Id) as EntityView;
                PopulateVariationProperties(variationView, variation, variationPropertyPolicy);
            }
        }

        /// <summary>
        /// Populates the variation properties in the entity view
        /// </summary>
        /// <param name="variationView">The variation view.</param>
        /// <param name="variation">The item variation component.</param>
        /// <param name="variationPropertyPolicy">The variation property policy.</param>
        protected virtual void PopulateVariationProperties(EntityView variationView, ItemVariationComponent variation, VariationPropertyPolicy variationPropertyPolicy)
        {
            foreach (var variationProperty in variationPropertyPolicy.PropertyNames)
            {
                var property = GetVariationProperty(variation, variationProperty);

                var insertIndex = variationView.Properties.Count > 0 ? variationView.Properties.Count - 1 : 0;
                variationView.Properties.Insert(insertIndex, new ViewProperty
                {
                    Name = variationProperty,
                    RawValue = property ?? string.Empty,
                    IsReadOnly = true
                });
            }
        }

        /// <summary>
        /// Gets the variation property
        /// </summary>
        /// <param name="variationComponent">The item variation component.</param>
        /// <param name="variationProperty">The name of the variation property.</param>
        /// <returns></returns>
        protected virtual object GetVariationProperty(ItemVariationComponent variationComponent, string variationProperty)
        {
            foreach (var component in variationComponent.ChildComponents)
            {
                var property = component.GetType().GetProperty(variationProperty);
                if (property != null)
                {
                    return property.GetValue(component);
                }
            }

            return null;
        }
    }
}