// The engine's item system is generic over the actor type; this game's
// actor is Game.Contracts.Entity. Aliases keep call sites short.
global using IItem = Engine.Items.IItem<Game.Contracts.Entity>;
global using EffectRegistry = Engine.Items.EffectRegistry<Game.Contracts.Entity>;
global using ItemFactory = Engine.Items.ItemFactory<Game.Contracts.Entity>;
