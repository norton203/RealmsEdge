// =====================================
// Realm's Edge - Sound Manager Module
// =====================================

const _sounds = {};
const _basePath = 'sounds/';
let _masterVolume = 0.8;
let _sfxVolume = 0.6;
let _musicVolume = 0.4;
let _isMuted = false;

// Maps C# SoundEffect enum names to file names
const _soundMap = {
    // Dice
    DiceRoll: { file: 'dice-roll.mp3', type: 'sfx' },

    // Combat
    CombatHit: { file: 'combat-hit.mp3', type: 'sfx' },
    CombatMiss: { file: 'combat-miss.mp3', type: 'sfx' },
    CombatCritical: { file: 'combat-critical.mp3', type: 'sfx' },
    CombatDeath: { file: 'combat-death.mp3', type: 'sfx' },

    // Character
    LevelUp: { file: 'level-up.mp3', type: 'sfx' },
    HealReceived: { file: 'heal-received.mp3', type: 'sfx' },
    BuffApplied: { file: 'buff-applied.mp3', type: 'sfx' },
    DebuffApplied: { file: 'debuff-applied.mp3', type: 'sfx' },

    // World
    DoorOpen: { file: 'door-open.mp3', type: 'sfx' },
    ChestOpen: { file: 'chest-open.mp3', type: 'sfx' },
    GoldPickup: { file: 'gold-pickup.mp3', type: 'sfx' },
    ItemPickup: { file: 'item-pickup.mp3', type: 'sfx' },

    // UI
    MenuSelect: { file: 'menu-select.mp3', type: 'sfx' },
    MenuBack: { file: 'menu-back.mp3', type: 'sfx' },
    QuestComplete: { file: 'quest-complete.mp3', type: 'sfx' },

    // Atmosphere
    TavernAmbience: { file: 'tavern-ambience.mp3', type: 'music' },
    DungeonAmbience: { file: 'dungeon-ambience.mp3', type: 'music' },
    BattleMusic: { file: 'battle-music.mp3', type: 'music' }
};

// Pre-load a sound into the cache
export function preload(soundName) {
    if (_sounds[soundName]) return;
    const entry = _soundMap[soundName];
    if (!entry) return;

    const audio = new Audio(_basePath + entry.file);
    audio.preload = 'auto';
    _sounds[soundName] = { audio, type: entry.type };
}

// Play a sound by its C# enum name
export function play(soundName) {
    if (_isMuted) return;

    let entry = _sounds[soundName];

    // Lazy load if not preloaded
    if (!entry) {
        preload(soundName);
        entry = _sounds[soundName];
    }

    if (!entry) return;

    const volume = entry.type === 'music' ? _musicVolume : _sfxVolume;
    entry.audio.volume = volume * _masterVolume;
    entry.audio.currentTime = 0;
    entry.audio.play().catch(() => {
        console.warn(`RealmsEdge SoundManager: Could not play ${soundName}`);
    });
}

// Stop a specific sound
export function stop(soundName) {
    const entry = _sounds[soundName];
    if (!entry) return;
    entry.audio.pause();
    entry.audio.currentTime = 0;
}

// Stop all sounds
export function stopAll() {
    Object.values(_sounds).forEach(entry => {
        entry.audio.pause();
        entry.audio.currentTime = 0;
    });
}

// Volume controls
export function setMasterVolume(volume) { _masterVolume = clamp(volume); }
export function setSfxVolume(volume) { _sfxVolume = clamp(volume); }
export function setMusicVolume(volume) { _musicVolume = clamp(volume); }
export function setMuted(muted) { _isMuted = muted; if (muted) stopAll(); }

function clamp(value) {
    return Math.min(1.0, Math.max(0.0, value));
}