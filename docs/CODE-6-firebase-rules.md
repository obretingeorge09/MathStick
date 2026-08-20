# MathStick — Firebase Realtime Database rules

> Published at console.firebase.google.com for project `plusminus-46df9`.
> `.indexOn: "elo"` is required or leaderboard queries sort on the phone.
> Known gap: no `.validate` constraints yet, so the client can write any value.

```json
{
  "rules": {
    "users": {
      ".read": "auth != null",
      "$uid": { ".write": "auth != null && auth.uid == $uid" }
    },

    "presence": {
      ".read": "auth != null",
      "$uid": { ".write": "auth != null && auth.uid == $uid" }
    },

    "matchmaking": {
      ".read": "auth != null",
      ".write": "auth != null"
    },

    "matches": {
      ".read": "auth != null",
      ".write": "auth != null"
    },

    "invites": {
      "$uid": {
        ".read": "auth != null && auth.uid == $uid",
        ".write": "auth != null"
      }
    },

    "friends": {
      ".read": "auth != null",
      ".write": "auth != null"
    },

    "stats": {
      ".read": "auth != null",
      "$uid": { ".write": "auth != null && auth.uid == $uid" }
    },

    "daily": {
      "$uid": {
        ".read": "auth != null && auth.uid == $uid",
        ".write": "auth != null && auth.uid == $uid"
      }
    },

    "leaderboard": {
      ".read": "auth != null",
      "global": {
        "$month": {
          ".indexOn": "elo",
          "$uid": { ".write": "auth != null && auth.uid == $uid" }
        }
      },
      "country": {
        "$cc": {
          "$month": {
            ".indexOn": "elo",
            "$uid": { ".write": "auth != null && auth.uid == $uid" }
          }
        }
      }
    }
  }
}
```
