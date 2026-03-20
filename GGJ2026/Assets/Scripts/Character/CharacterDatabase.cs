using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialog/Character Database")]
public class CharacterDatabase : ScriptableObject
{
    public List<CharacterEntry> characters;
}


