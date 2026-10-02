# Our Happy Home
## Game Design Document

**Tagline:** *Every day is a story. Every family is an adventure.*

## 1. Game Overview

**Genre:** Family Life Simulator, House Building, Cozy Adventure, Light Survival  
**Mode:** Single Player, with potential future co-op support  
**Perspective:** Third-person  
**Visual Style:** Colorful stylized 3D, cozy, expressive, family-friendly  

### High Concept

*Our Happy Home* is a family life simulation game about a 10-year-old boy living with his parents, older sister, and younger sister. Players build and decorate a home, experience everyday family life, develop skills and relationships, explore the neighborhood, and respond to unexpected events.

The central objective is simple:

> Build a happy home, create memorable family stories, and make sure nobody gets left behind.

The game combines four main pillars:

1. **BUILD** - Build, expand, furnish, and customize the family home.
2. **LIVE** - Experience daily routines, relationships, school, cooking, hobbies, celebrations, and family activities.
3. **EXPLORE** - Visit school, shops, parks, forests, beaches, camping areas, and other locations.
4. **PROTECT** - Keep the family safe during accidents, extreme weather, wild-animal encounters, suspicious visitors, and other emergencies.

Combat is not the main focus. Dangerous situations emphasize escape, protection, alarms, securing the home, rescuing family members, and making threats leave.

---

## 2. Family

The family consists of five independently simulated characters.

### Father

**Age:** 25  
**Appearance:** Short-sleeved shirt under a jacket, denim shorts, belt, and cap.

**Core Skills:**
- Repair
- Building
- Driving
- Outdoor activities
- Family protection

Father is particularly useful when something in the house breaks or an emergency requires physical activity. Failed repairs can also produce humorous situations, such as a broken faucet spraying water through the kitchen.

### Mother

**Age:** 24

**Core Skills:**
- Cooking
- Shopping
- Gardening
- Household management
- Family care

Mother has her own hobbies, friendships, goals, activities, and potentially a career rather than existing only as a household NPC.

### Older Sister

**Age:** 11  
**Gender:** Female

**Appearance:**
- Straight hair
- Two shell-shaped hair clips
- Smiling expression
- Light-blue top
- Bow-shaped buttons
- Long skirt
- Light-pink shoes

**Interests:**
- Drawing
- Music
- Fashion
- Gardening
- Photography
- Animals

### Main Character

**Age:** 10  
**Gender:** Male

**Appearance:**
- Dark-red shirt with an "I Love Mom" design
- Navy-blue jacket
- Casual trousers
- Sneakers

The main character is the most flexible family member. Possible skills include cooking, cycling, swimming, reading, repairing, gardening, animal care, sports, crafting, and basic first aid.

### Younger Sister

**Age:** 8  
**Gender:** Female

**Personality:** Cheerful, intelligent, curious, helpful, and loves reading English books.

**Appearance:**
- Dark-red beanie
- Plain white shirt
- Dark-red jacket
- White bow-shaped buttons

**Favorite Activity:** Helping Mother prepare breakfast and other food.

Recipes can include:
- Fried rice
- Eggs
- Chicken
- Pancakes
- Cupcakes
- Pudding
- Warm drinks
- Milk
- Water

Repeated cooking increases her Cooking skill. At low levels, harmless and funny mistakes can happen, such as burnt pancakes or flour covering the kitchen counter.

---

## 3. Core Gameplay Loop

The primary gameplay loop is:

```text
Wake Up
   |
Morning Routine
   |
Eat Breakfast Together
   |
School / Work / Activities
   |
Earn Money and Improve Skills
   |
Shop / Explore / Socialize
   |
Improve the House
   |
Family Activities
   |
Possible Random Event
   |
Dinner
   |
Sleep
   |
Next Day
```

Long-term progression follows:

```text
Build -> Live -> Earn -> Upgrade -> Care -> Explore -> Respond to Events -> Create Memories
```

---

## 4. Living Family AI

One of the game's key differentiators is that family members should feel alive rather than waiting for player commands.

Each character maintains:

- Personality
- Likes and dislikes
- Mood
- Memories
- Relationships
- Needs
- Daily schedule
- Goals
- Fears
- Skills
- Hobbies

Example autonomous morning:

```text
06:00 - Mother wakes up and enters the kitchen.
06:05 - Younger sister wakes up and offers to help.
06:10 - Father takes a shower.
06:20 - Older sister is still asleep.
06:30 - Mother makes fried rice while Younger Sister prepares eggs.
06:40 - Father reads while waiting for breakfast.
06:45 - Older Sister finally wakes up.
07:00 - The family eats breakfast together.
```

Characters can remember meaningful experiences. If Younger Sister previously made pancakes with Mother, she may later suggest making them again. Gifts, trips, arguments, achievements, celebrations, and emergencies can also become memories.

---

## 5. Character Needs

Every character has simulated needs:

- Health
- Hunger
- Energy
- Happiness
- Hygiene
- Sleep
- Fun
- Social / Family connection

Low needs affect behavior. A tired character may yawn, move slowly, sit down, and eventually fall asleep on a sofa.

---

## 6. Relationships

Relationships exist independently between family members.

Example:

```text
Younger Sister -> Older Sister: 80%
Player -> Father:                92%
Player -> Younger Sister:        61%
```

Relationships improve through:

- Talking
- Playing
- Eating together
- Helping each other
- Gifts
- Shared hobbies
- Trips
- Solving problems together

Characters can become happy, sad, annoyed, argue, apologize, and reconcile.

---

## 7. House Building

Players can build and expand their home over time.

Possible progression:

```text
Small Home -> Family Home -> Large Home -> Dream Home
```

Possible rooms include:

- Parents' bedroom
- Children's bedrooms
- Kitchen
- Dining room
- Living room
- Bathrooms
- Garage
- Library
- Playroom
- Workshop
- Backyard
- Garden
- Swimming pool
- Tree house
- Secret room

Furniture should be interactive whenever practical. A blender can make drinks, a stove can cook food, a bookshelf can be read, and a television can be watched by the family.

---

## 8. Economy

The family uses money for:

- Food
- Clothing
- Furniture
- House expansion
- Vehicles
- Hobbies
- Entertainment
- Vacations

Parents may have jobs. Children can receive allowances or earn small rewards through family-friendly activities such as selling garden produce, artwork, lemonade, or cupcakes, or helping with pets.

---

## 9. Cooking

Cooking is both a survival need and a family bonding activity.

Recipes have ingredients, preparation steps, cooking time, skill requirements, and quality levels.

Possible quality states:

```text
Failed -> Poor -> Normal -> Delicious -> Perfect
```

Family members can cook together, teach recipes, discover new dishes, or develop favorites.

---

## 10. School

Because the protagonist is 10, school forms an important part of progression.

Potential subjects include:

- English
- Mathematics
- Science
- Art
- Sports
- Computer studies

School can contain short activities and mini-games rather than simply removing the character from the simulation.

Academic development may unlock practical abilities. For example, Science knowledge combined with Repair skill may allow the character to understand simple household electronics.

---

## 11. Calendar and Daily Life

A calendar keeps the world varied.

Possible recurring activities:

- School days
- Family movie night
- Weekend shopping
- Swimming
- Camping
- Pancake morning
- Picnic
- Birthdays
- Festivals
- Holidays
- School events

Example calendar:

```text
JANUARY
03 - First School Day
08 - House Inspection
12 - Younger Sister's Birthday
18 - Family Camping
24 - Town Festival
31 - Family Movie Night
```

---

## 12. Weather

Weather affects character behavior and available activities.

Possible weather:

- Sunny
- Rain
- Thunderstorm
- Fog
- Strong wind
- Severe storm

Rain may make outdoor characters run home. Thunderstorms can cause temporary power outages. The family may gather indoors with flashlights until electricity returns.

---

## 13. Random Events

Random events prevent everyday life from becoming repetitive.

### Common Events

- Cat enters the house
- Light bulb breaks
- Faucet leaks
- Food gets burnt
- A household appliance stops working

### Uncommon Events

- Animal steals food
- Local flooding
- Power outage
- Dangerous animal enters the yard
- Important household item breaks

### Rare Events

- Suspicious stranger around the home
- Attempted burglary
- Small house fire
- Severe storm

### Multi-Day Events

Some rare events become larger scenarios.

Example: **The Great Storm**

```text
Storm Warning
   |
Inspect the House
   |
Secure Outdoor Objects
   |
Prepare Food and Water
   |
Prepare Flashlights
   |
Make Sure Everyone Is Home
   |
Storm Arrives
   |
Protect the Family
   |
Inspect Damage
   |
Repair and Recover
```

---

## 14. Home Safety and Defense

Danger events focus on protecting the family rather than graphic violence.

Available responses can include:

- Locking doors
- Closing windows
- Blocking access to unsafe areas
- Activating alarms
- Turning on exterior lights
- Monitoring cameras
- Moving younger family members to safe areas
- Distracting or driving away threats
- Escaping through alternate routes
- Using appropriate household safety equipment

Players must balance actions against stamina and time.

---

## 15. Stamina

Physical activities consume stamina.

```text
100% - Ready
 60% - Tired
 20% - Exhausted
  0% - Must recover
```

When exhausted, a character temporarily cannot perform demanding actions and needs time to recover. This creates tension during emergencies without requiring graphic combat mechanics.

---

## 16. Family Down / Rescue System

Dangerous situations can place a family member in a **Down**, **Trapped**, or **Needs Help** state.

Example:

```text
SISTER NEEDS HELP

Safe in: 00:42

Find her and bring her to a safe location.
```

Different family members can contribute according to their abilities. Father might be better at physically demanding rescue tasks, while Younger Sister's intelligence may help her remember a safe route or locate emergency equipment.

---

## 17. Failure Condition

The central rule is:

> Nobody Gets Left Behind.

If the player fails to keep every family member safe during a major scenario, the scenario ends and can be restarted.

```text
FAMILY FAILED

Nobody Gets Left Behind.

Restart Event
```

The game avoids graphic death scenes and instead emphasizes rescue, family attachment, and responsibility.

---

## 18. Neighborhood and World

The game gradually expands beyond the home.

Possible world structure:

```text
                 Mountain / Camping
                        |
Forest -------- Neighborhood -------- School
                        |
                    Downtown
                   /        \
              Clinic       Mall
                 |
               Beach
```

Locations may include:

- School
- Supermarket
- Mall
- Clinic
- Restaurants
- Arcade
- Swimming pool
- Beach
- Forest
- Camping ground
- Theme park
- Community park

---

## 19. Pets

Possible pets include:

- Dog
- Cat
- Rabbit
- Hamster
- Fish

Pets have needs, personalities, happiness, energy, and trust.

Example:

```text
BROWNIE

Hunger       70%
Happiness    90%
Energy       50%
Trust        80%
```

Pets can interact with the simulation. A dog, for example, may bark when noticing unusual activity in the yard.

---

## 20. Family Events

Special events create memorable stories:

- Birthdays
- Holidays
- Vacations
- Camping trips
- Theme park visits
- BBQs
- Movie nights
- Costume parties
- Picnics
- School achievements

Photographs from important moments are stored in a **Family Album**.

Over dozens of hours, the album becomes a visual history of the player's unique family story.

---

## 21. Progression Structure

### Chapter 1 - Our Little Home

The family moves into a small home with basic furniture and limited money.

### Chapter 2 - New Neighborhood

The family meets neighbors and begins exploring the town.

### Chapter 3 - Growing Together

Characters develop skills, friendships, interests, and stronger relationships.

### Chapter 4 - Our Dream Home

The family can afford significant renovations and build a personalized dream house.

### Chapter 5 - Big Adventures

Camping, vacations, major weather events, and larger family stories become available.

### End Game - Family Sandbox

After the main progression, the game continues indefinitely as an open-ended family life simulator.

---

## 22. Emergent Storytelling

Instead of relying entirely on scripted missions, gameplay systems combine to create stories.

Example:

```text
It starts raining.
        |
Power goes out.
        |
Younger Sister becomes scared.
        |
Player searches for a flashlight.
        |
Father checks the electrical system.
        |
Mother prepares warm drinks.
        |
Everyone gathers in the living room.
        |
Power returns.
        |
The evening becomes a positive family memory.
```

Another player's version of the same event could unfold completely differently.

---

## 23. Dynamic Memories

Significant events generate family memories.

A memory may contain:

- Characters involved
- Location
- Event type
- Emotional outcome
- Date
- Photograph
- Relationship changes

Memories can influence future dialogue and behavior.

This system helps transform ordinary simulation events into a continuous family story.

---

## 24. Art Direction

The visual direction should be soft, colorful, cozy, and expressive rather than photorealistic.

Characters can use slightly exaggerated proportions and strong facial expressions to make emotions readable.

### Day

Warm sunlight, bright colors, lively neighborhoods, children playing, and active outdoor spaces.

### Night

Warm interior lighting contrasts with cooler outdoor colors, reinforcing the feeling that the home is a safe and comfortable place.

### Rain

Window reflections, rain audio, family members gathering indoors, and warm lighting can create a strong cozy atmosphere.

The house itself should feel like an important character in the experience.

---

## 25. Audio Direction

Audio should reinforce family life.

Important sounds include:

- Rain hitting windows
- Kitchen activity
- Footsteps in different rooms
- Doors opening and closing
- Family conversations
- Television audio
- Birds in the morning
- Neighborhood ambience
- Appliances
- Pets
- Weather

Music should dynamically shift between cozy daily life, exploration, celebrations, emotional family moments, and tense emergency events.

---

## 26. UI Direction

The UI should remain easy to understand and usable by younger players.

Core interface elements:

- Current character
- Needs
- Stamina
- Mood
- Current objective
- Time and date
- Weather
- Family status
- Money

A family panel allows quick checking of every family member's status and location.

---

## 27. Accessibility and Family-Friendly Design

Recommended options include:

- Adjustable difficulty
- Simplified controls
- Reading assistance
- Subtitles
- Color accessibility settings
- Reduced emergency intensity
- Optional tutorials
- No graphic injury imagery
- Pause during single-player gameplay

A **Cozy Mode** could significantly reduce dangerous events for players who mainly want house building and family simulation.

An **Adventure Mode** could increase environmental challenges and emergency events while preserving the family-friendly presentation.

---

## 28. Key Differentiators

The core selling points are:

1. A house-building game where the house is actually lived in.
2. Five family members with autonomous routines and personalities.
3. Relationships and memories that develop over time.
4. Everyday life mixed with unpredictable adventures.
5. Family cooperation instead of combat as the central survival mechanic.
6. An evolving home that visually represents the family's progress.
7. Emergent storytelling generated from interacting simulation systems.

---

## 29. Design Philosophy

The player should gradually become emotionally attached to the family.

Cooking pancakes with Younger Sister, decorating Older Sister's bedroom, repairing something with Father, gardening with Mother, adopting a pet, celebrating birthdays, and going camping should make emergency situations meaningful because the characters matter to the player.

The game's tension therefore does not come primarily from stronger enemies.

It comes from having something worth protecting.

---

## 30. Potential Titles

- **Our Happy Home**
- Together at Home
- My Family Story
- Home Together
- Family Days
- Our Little World
- Home Sweet Adventure
- Together, Always

### Recommended Title

# Our Happy Home

> *Every day is a story. Every family is an adventure.*

---

## 31. Core Vision Statement

*Our Happy Home* should make ordinary family moments feel meaningful. Building a bedroom, making breakfast, walking to school, adopting a dog, surviving a thunderstorm, celebrating a birthday, or helping a sibling should all contribute to one continuous story created by the player.

The ultimate goal is not to defeat a final enemy.

The ultimate goal is to look back at the family's home, relationships, photographs, memories, adventures, and achievements and feel:

> **We built this life together.**
