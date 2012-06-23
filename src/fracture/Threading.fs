//----------------------------------------------------------------------------
//
// Copyright (c) 2011-2012 Dave Thomas (@7sharp9) 
//                         Ryan Riley (@panesofglass)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------
module Fracture.Threading

open System.Threading

let inline threadsafeDecrement (a:int ref) = Interlocked.Decrement(a) |> ignore
let inline threadsafeIncrement (a:int ref) = Interlocked.Increment(a) |> ignore
let inline (!--) (a:int ref) = threadsafeDecrement a
let inline (!++) (a:int ref) = threadsafeIncrement a