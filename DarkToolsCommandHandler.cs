using Eco.Core.Plugins;
using Eco.Gameplay.Aliases;
using Eco.Gameplay.Auth;
using Eco.Gameplay.Components;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Economy;
using Eco.Gameplay.Items;
using Eco.Gameplay.Objects;
using Eco.Gameplay.Players;
using Eco.Gameplay.Property;
using Eco.Gameplay.Systems;
using Eco.Gameplay.Systems.Messaging.Chat.Commands;
using Eco.Gameplay.Systems.NewTooltip.TooltipLibraryFiles;
using Eco.Gameplay.Systems.TextLinks;
using Eco.Gameplay.Utils;
using Eco.Shared;
using Eco.Shared.IoC;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Math;
using Eco.Shared.Serialization;
using Eco.Shared.Utils;
using Eco.Shared.Voxel;
using System.Text;

namespace DarkTools {
    [ChatCommandHandler]
    public static class DarkToolsCommandHandler {
        [ChatCommand("Shows commands available from the DarkTools mod", "dt")]
        public static void DarkTools() { }

        [ChatSubCommand(nameof(DarkTools), "READ ME FIRST!", ChatAuthorizationLevel.User)]
        public static void Help(User user) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Localizer.DoStr("Welcome to DarkTools!"));
            sb.AppendLine(Localizer.DoStr("This mod provides some commands, that will make life and trading easier."));

            user.TempServerMessage(sb.ToStringLoc());
        }

        [ChatSubCommand(nameof(DarkTools), "Command to check where you can sell an item, and how many the store can fit and afford.", "wts", ChatAuthorizationLevel.User)]
        public static void WantToSell(User user, string itemName) {
            var authManager = ServiceHolder<IAuthManager>.Obj;
            var lookup = Item.AllItemsExceptHidden.ToDictionary(i => i.DisplayName.ToString().ToLower());
            if (!lookup.ContainsKey(itemName.ToLower()))
                return;
            var item = lookup[itemName.ToLower()];

            var sb = new StringBuilder();
            bool first = true;
            //sb.AppendLine($"Item to check: {item.MarkedUpName}");
            var stores = WorldObjectUtil.AllObjsWithComponent<StoreComponent>().Where(store => store.OnOff.On).ToList();
            if (stores.Count == 0)
                return;
            LocString currency = new LocString();
            foreach (var store in stores.OrderBy(s => s.Currency.IsPlayerCredit).ThenBy(s => s.CurrencyName)) {
                // ignore unauthorized stores
                if (!authManager.IsAuthorized(store.Parent, user, AccessType.ConsumerAccess, null))
                    continue;
                var bestBuyOffer = store.StoreData.BuyOffers.Where(o => o.Stack.Item.TypeID == item.TypeID).OrderByDescending(o => o.Price).FirstOrDefault();
                if (bestBuyOffer == null)
                    continue;
                if (currency != store.Currency.MarkedUpName) {
                    currency = store.Currency.MarkedUpName;
                    if (!first)
                        sb.AppendLine();
                    else
                        first = false;
                    sb.AppendLine(Text.Bold($"Sell {item.MarkedUpName} for {currency}"));
                    sb.AppendLine();
                }
                var limit = bestBuyOffer.Limit;
                int itemsInStock = 0;
                int itemsFit = 0;
                foreach (var i in store.OutputInventory.AllInventories) {
                    if (i is not AuthorizationInventory)
                        continue;
                    var maxStackSize = i.GetMaxAcceptedVal(item, 0);
                    foreach (var s in i.Stacks) {
                        if (s.Item == null)
                            itemsFit += maxStackSize;
                        else if (s.Item.TypeID == item.TypeID) {
                            itemsInStock += s.Quantity;
                            itemsFit += maxStackSize - s.Quantity;
                        }
                    }
                }
                if (limit > 0 && itemsInStock >= limit)
                    continue;
                int toSell = 999;
                if (limit > 0) {
                    toSell = limit - itemsInStock;
                }
                var canAfford = store.BankAccount.GetCurrencyHoldingVal(store.Currency) / bestBuyOffer.Price;
                if (float.IsInfinity(canAfford))
                    canAfford = 999;
                toSell = Math.Min(toSell, (int)canAfford);
                sb.AppendLine($"{Math.Min(itemsFit, toSell)}\tfor\t{Text.Bold(Text.Color(Color.Yellow, bestBuyOffer.Price))} @ {store.Parent.MarkedUpName}");

            }
            user.Player.OpenInfoPanel(item.DisplayName, sb.ToString(), "Trades");
            Text.CopyToClipBoard("woops", null, "what the fuck?");
        }
    }
}
